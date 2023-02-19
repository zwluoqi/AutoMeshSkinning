using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Content.Scritps
{
    public static class CustomMeshUtil
    {
        
        public struct BoneData
        {
            public int boneId;
            public string boneName;
            public HumanBodyBones humanBodyBones;
            public HumanBodyBones rootHumanBodyBones;
            public string NameBonePath;
            public Vector3 position;

            public override string ToString()
            {
                return $"{this.boneId} ,{this.humanBodyBones},{this.boneName}:'{rootHumanBodyBones}'/{this.NameBonePath}";
            }
        }

        private static HumanBodyBones FindHumanBodyBoneByName(this HumanBone[] humanBones,string boneName)
        {
            foreach (var human in humanBones)
            {
                if (human.boneName == boneName)
                {
                    if (Enum.TryParse<HumanBodyBones>(human.humanName.Replace(" ",""), out var humanBodyBone))
                    {
                        return (humanBodyBone);
                    }
                }
            }

            return (HumanBodyBones.LastBone);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="animator"></param>
        /// <param name="mesh"></param>
        /// <param name="bones">key:原始boneid,bone信息</param>
        /// <returns></returns>
        public static Dictionary<int,BoneData>  GetMeshBoneWeighs(Animator animator,Mesh mesh, List<Transform> bones)
        {
            var boneWeights = mesh.boneWeights;
            Dictionary<int,BoneData> boneMaps = new Dictionary<int, BoneData>();
            HumanBone[] humanBones = animator.avatar.humanDescription.human;
            Action<int> insertBone = delegate(int boneIndex)
            {
                if (!boneMaps.ContainsKey(boneIndex))
                {
                    var boneName = bones[boneIndex].name;
                    var humanBodyBones = humanBones.FindHumanBodyBoneByName(boneName);
                    
                    var boneMap = new BoneData()
                    {
                        boneId = boneIndex, 
                        boneName = boneName, 
                        humanBodyBones = humanBodyBones,
                        position = bones[boneIndex].position,
                    };
                    if (humanBodyBones == HumanBodyBones.LastBone)
                    {
                        boneMap.rootHumanBodyBones =
                            GetRootHumanBodyBone(ref boneMap.NameBonePath,humanBones,animator.transform, bones[boneIndex]);
                    }
                    else
                    {
                        boneMap.rootHumanBodyBones = HumanBodyBones.LastBone;
                    }

                    boneMaps[boneIndex] = boneMap;
                }
            };
            
            foreach (var boneWeight in boneWeights)
            {
                insertBone(boneWeight.boneIndex0);
                insertBone(boneWeight.boneIndex1);
                insertBone(boneWeight.boneIndex2);
                insertBone(boneWeight.boneIndex3);
            }

            return boneMaps;
        }

        private static HumanBodyBones GetRootHumanBodyBone(ref string bonePath, HumanBone[] humanBones,Transform animatorRoot,Transform bone)
        {
            var humanBodyBones = humanBones.FindHumanBodyBoneByName(bone.name);
            if (humanBodyBones != HumanBodyBones.LastBone)
            {
                return humanBodyBones;
            }
            bonePath = bone.name + "/" + bonePath;

            var parent = bone.transform.parent;

            if (parent == null)
            {
                return HumanBodyBones.LastBone;
            }
            
            if (animatorRoot == parent)
            {
                return HumanBodyBones.LastBone;
            }

            return GetRootHumanBodyBone(ref bonePath,humanBones,animatorRoot, parent);
        }


        public class CustomAvatar
        {
            public Animator animatar;
            // public List<Transform> bones;
            public Transform rootBone;//maybe to search
        }
        
        public class CustomSMRBindResult
        {
            public SkinnedMeshRenderer skinMeshRender;
            public List<Transform> bones = new List<Transform>();
        }

        

        /// <summary>
        /// 将SMR绑定到一个新的animator下的骨架下面,返回新的SMR和新的蒙皮骨骼数组
        /// 请记录新的蒙皮骨骼数据，方便下次传入
        /// </summary>
        /// <param name="skinnedMeshRenderer"></param>
        /// <param name="sourceAvatar"></param>
        /// <param name="targetAvatar"></param>
        /// <returns></returns>
        public static CustomSMRBindResult BindSMRToTargetAvatar(SkinnedMeshRenderer skinnedMeshRenderer,CustomAvatar sourceAvatar, CustomAvatar targetAvatar)
        {
            var sourceBones = skinnedMeshRenderer.bones;
            var sourceBindPose = skinnedMeshRenderer.sharedMesh.bindposes;
            var targetBones = Util.GetTransforms(targetAvatar.animatar.transform);
            
            var newSkinMeshRender = Object.Instantiate(skinnedMeshRenderer, targetAvatar.animatar.transform, true);
            var sharedMesh  = Object.Instantiate(skinnedMeshRenderer.sharedMesh);


            //1.获取原始sharemesh的在原始animator上骨骼映射数据
            var sourceBoneWeight = GetMeshBoneWeighs(sourceAvatar.animatar, sharedMesh, sourceBones.ToList());
            // StringBuilder stringBuilder = new StringBuilder();
            // foreach (var bone in sourceBoneWeight.Values)
            // {
            //     stringBuilder.AppendLine(bone.ToString());
            // }
            // Debug.LogWarning(stringBuilder.ToString());

            
            //2.通过骨骼名称或者原mesh在新animator上的骨骼映射
            List<Transform> newBindBones = new List<Transform>();
            Dictionary<int, MapBone> mapBoneId = GetOrCreateMapBone(sourceBoneWeight.Values.ToArray(),sourceAvatar.animatar,targetAvatar.animatar,targetBones,out newBindBones);

            //2.重置mesh的骨骼权重数据及顶点位置
            var boneWeights = sharedMesh.boneWeights;
            var vertices = sharedMesh.vertices;
            for (int b = 0; b < boneWeights.Length; b++)
            {
                var sourceBoneWeightPos0 = sourceBoneWeight[boneWeights[b].boneIndex0].position* boneWeights[b].weight0;
                var targetBoneWeightPos0 = mapBoneId[boneWeights[b].boneIndex0].boneTrans.position* boneWeights[b].weight0;
                var sourceBoneWeightPos1 = sourceBoneWeight[boneWeights[b].boneIndex1].position* boneWeights[b].weight1;
                var targetBoneWeightPos1 = mapBoneId[boneWeights[b].boneIndex1].boneTrans.position* boneWeights[b].weight1;
                var sourceBoneWeightPos2 = sourceBoneWeight[boneWeights[b].boneIndex2].position* boneWeights[b].weight2;
                var targetBoneWeightPos2 = mapBoneId[boneWeights[b].boneIndex2].boneTrans.position* boneWeights[b].weight2;
                var sourceBoneWeightPos3 = sourceBoneWeight[boneWeights[b].boneIndex3].position* boneWeights[b].weight3;
                var targetBoneWeightPos3 = mapBoneId[boneWeights[b].boneIndex3].boneTrans.position* boneWeights[b].weight3;

                var offset =
                    (targetBoneWeightPos0 + targetBoneWeightPos1 + targetBoneWeightPos2 + targetBoneWeightPos3) -
                    (sourceBoneWeightPos0 + sourceBoneWeightPos1 + sourceBoneWeightPos2 + sourceBoneWeightPos3);
                
                    
                var sourceBoneIndex = boneWeights[b].boneIndex0;
                boneWeights[b].boneIndex0 = mapBoneId[sourceBoneIndex].newBoneId;

                sourceBoneIndex = boneWeights[b].boneIndex1;
                boneWeights[b].boneIndex1 = mapBoneId[sourceBoneIndex].newBoneId;
                
                sourceBoneIndex = boneWeights[b].boneIndex2;
                boneWeights[b].boneIndex2 = mapBoneId[sourceBoneIndex].newBoneId;
                
                sourceBoneIndex = boneWeights[b].boneIndex3;
                boneWeights[b].boneIndex3 = mapBoneId[sourceBoneIndex].newBoneId;

                vertices[b] += offset;
            }
            
            
            //add new skeleton
            var human =  targetAvatar.animatar.avatar.humanDescription;
            var skeleton = new List<SkeletonBone>();
            skeleton.AddRange(human.skeleton);
            bool needNewAvatar = false;
            foreach (var bindBone in newBindBones)
            {
                var ske =  skeleton.FirstOrDefault(skeletonBone => skeletonBone.name == bindBone.name);
                if (string.IsNullOrEmpty(ske.name))
                {
                    //add new skeleton
                    skeleton.Add(new SkeletonBone()
                    {
                        name = bindBone.name,
                        position = bindBone.localPosition,
                        rotation = bindBone.localRotation,
                        scale = bindBone.localScale
                    });
                    needNewAvatar = true;
                }
            }

            if (needNewAvatar)
            {
                human.skeleton = skeleton.ToArray();
                Avatar avatar = AvatarBuilder.BuildHumanAvatar(targetAvatar.animatar.gameObject, human);
                avatar.name = targetAvatar.animatar.avatar.name;
                targetAvatar.animatar.avatar = avatar;
            }

            //reset bindposes and bone
            //这里不直接冲bones获得worldtolocal的matrix，因为当前的Tpose中skeleton的数据并不代表当时bindpose的数据
            //所以这里直接取mesh里面bindposes数据
            // var bindposes = new Matrix4x4[newBindBones.Count];//customSmrBindResult.bones.Select(x => x.transform.worldToLocalMatrix).ToArray();
            // for (int i = 0; i < newBindBones.Count; i++)
            // {
            //     var kv = mapBoneId.FirstOrDefault(obj => obj.Value.newBoneId == i);
            //     if (kv.Value == null)
            //     {
            //         Debug.LogError($"骨骼是新建的{newBindBones[i].name}, 沒法找到原始的bindpose 所以這裏直接使用worldToLocalMatrix");
            //         bindposes[i] = newBindBones[i].worldToLocalMatrix;
            //     }
            //     else
            //     {
            //         var sourceBoneId = kv.Key;
            //         bindposes[i] = sourceBindPose[sourceBoneId];
            //     }
            // }
            var bindposes = newBindBones.Select(x => x.transform.worldToLocalMatrix).ToArray();
            
            
            sharedMesh.vertices = vertices;
            sharedMesh.boneWeights = boneWeights;
            sharedMesh.bindposes = bindposes;
            sharedMesh.RecalculateBounds();
            newSkinMeshRender.sharedMesh = sharedMesh;
            
            newSkinMeshRender.bones = newBindBones.ToArray();
            var newRootBone = GetChildBoneRecursion(targetAvatar.animatar.transform, sourceAvatar.rootBone.name);
            newSkinMeshRender.rootBone =  newRootBone;
            newSkinMeshRender.ResetBounds();

            CustomSMRBindResult customSmrBindResult = new CustomSMRBindResult()
            {
                skinMeshRender = newSkinMeshRender,
                bones = newBindBones.ToList(),
            };
            return customSmrBindResult;
        }

        public class MapBone
        {
            public int newBoneId;
            public Transform boneTrans;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="boneMap"></param>
        /// <param name="sourceAvatar"></param>
        /// <param name="targetAvatar"></param>
        /// <param name="targetTrans"></param>
        /// <param name="resultBones"></param>
        /// <returns>key 原始boneid，value 新的bone信息</returns>
        /// <exception cref="NotImplementedException"></exception>
        private static Dictionary<int, MapBone> GetOrCreateMapBone(BoneData[] boneMap,Animator sourceAvatar,Animator targetAvatar,List<Transform> targetTrans,out List<Transform> resultBones)
        {
            Dictionary<int, MapBone> mapBoneId = new Dictionary<int, MapBone>();
            resultBones = new List<Transform>();
            var skeleton = sourceAvatar.avatar.humanDescription.skeleton;
            foreach (var bone in boneMap)
            {
                if (bone.humanBodyBones != HumanBodyBones.LastBone)
                {
                    var targetBoneTrans = targetAvatar.GetBoneTransform(bone.humanBodyBones);
                    if (targetBoneTrans == null)
                    {
                        if (bone.humanBodyBones == HumanBodyBones.Chest)
                        {
                            Debug.LogError($"{targetAvatar.name}不存在对应的HumanBone:{bone.humanBodyBones},所以我去找UpperChest");
                            targetBoneTrans = targetAvatar.GetBoneTransform(HumanBodyBones.UpperChest);
                        }
                    }
                    
                    
                    if (targetBoneTrans == null)
                    {
                        throw new NotImplementedException($"{targetAvatar.name}不存在对应的HumanBone 暂时无能为力:"+bone.humanBodyBones);
                    }

                    var boneIdByName = GetBoneIndex(resultBones, targetBoneTrans.name);
                    if (boneIdByName.Item1 >= 0)
                    {
                        mapBoneId[bone.boneId] = new MapBone()
                        {
                            newBoneId  = boneIdByName.Item1,
                            boneTrans = targetBoneTrans
                        };
                        continue;
                    }
                    resultBones.Add(targetBoneTrans);
                    mapBoneId[bone.boneId] = new MapBone()
                    {
                        newBoneId  = resultBones.Count-1,
                        boneTrans = targetBoneTrans
                    };
                }
                else
                {
                    var boneIdByName = GetBoneIndex(resultBones, bone.boneName);
                    if (boneIdByName.Item1 >= 0)
                    {
                        mapBoneId[bone.boneId] = new MapBone()
                        {
                            newBoneId  = boneIdByName.Item1,
                            boneTrans = boneIdByName.Item2
                        };
                        continue;
                    }
                    boneIdByName = GetBoneIndex(targetTrans, bone.boneName);
                    if (boneIdByName.Item1 >= 0)
                    {
                        resultBones.Add(boneIdByName.Item2);
                        mapBoneId[bone.boneId] = new MapBone()
                        {
                            newBoneId  = resultBones.Count-1,
                            boneTrans = boneIdByName.Item2
                        };
                        continue;
                    }
                    
                    if (bone.rootHumanBodyBones != HumanBodyBones.LastBone)
                    {
                        var rootBoneTrans = targetAvatar.GetBoneTransform(bone.rootHumanBodyBones);
                        if (rootBoneTrans == null)
                        {
                            throw new NotImplementedException(
                                $"{targetAvatar.name}不存在对应的HumanBone 暂时无能为力:" + bone.rootHumanBodyBones);
                        }

                        var childBoneNames = bone.NameBonePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        
                        int startParentIndex = 0;
                        var curRootBoneTrans = rootBoneTrans;
                        for (int i = childBoneNames.Length-1; i >= 0; i--)
                        {
                            var childBone = GetChildBoneRecursion(targetAvatar.transform, childBoneNames[i]);
                            if (childBone)
                            {
                                startParentIndex = i+1;
                                curRootBoneTrans = childBone;
                                break;
                            }
                        }

                        for (int i = startParentIndex; i < childBoneNames.Length; i++)
                        {
                            var childBone = GetChildBoneRecursion(curRootBoneTrans, childBoneNames[i]);
                            if (childBone == null)
                            {
                                var ske =  skeleton.First(skeletonBone => skeletonBone.name == childBoneNames[i]);
                                childBone = CreateChildBone(curRootBoneTrans, childBoneNames[i],ske.position,ske.rotation);
                                resultBones.Add(childBone);
                            }
                            curRootBoneTrans = childBone;
                        }
                            
                        //获取新的id
                        var newboneIdByName = GetBoneIndex(resultBones, bone.boneName);
                        mapBoneId[bone.boneId] = new MapBone()
                        {
                            newBoneId  = newboneIdByName.Item1,
                            boneTrans = newboneIdByName.Item2
                        };
                    }
                    else
                    {
                        var ske =  skeleton.First(skeletonBone => skeletonBone.name == bone.boneName);
                        var childBone = CreateChildBone(targetAvatar.transform, bone.boneName, ske.position,ske.rotation);
                        resultBones.Add(childBone);
                        
                        mapBoneId[bone.boneId] = new MapBone()
                        {
                            newBoneId  = resultBones.Count-1,
                            boneTrans = childBone
                        };
                        
                        Debug.LogError(
                            "rootHumanBodyBones为nil 且没有同名bone 直接在根节点下新建 for:"+bone.ToString());
                    }
                }
            }

            return mapBoneId;
        }

        private static Transform CreateChildBone(Transform curRootBoneTrans, string childBoneName,Vector3 pos,Quaternion elu)
        {
            Debug.LogWarning($"创建骨骼点:" + childBoneName);
            var child = new GameObject(childBoneName);
            child.transform.SetParent(curRootBoneTrans);
            Util.ResetTrans(child.transform);
            child.transform.localPosition = pos;
            child.transform.localRotation = elu;
            return child.transform;
        }

        private static Transform GetChildBoneRecursion(Transform rootBoneTrans, string childBoneName)
        {
            var childCount = rootBoneTrans.childCount;
            for (int i = 0; i<childCount; i++)
            {
                var childBone = rootBoneTrans.GetChild(i);
                if (childBone.name.Equals(childBoneName))
                {
                    return childBone;
                }

                childBone = GetChildBoneRecursion(childBone, childBoneName);
                if (childBone != null)
                {
                    return childBone;
                }
            }

            return null;
        }

        // private static int GetBoneIndex(List<Transform> targetAvatarBones, Transform targetBoneTrans)
        // {
        //     for (int i = 0; i < targetAvatarBones.Count; i++)
        //     {
        //         if (targetAvatarBones[i] == targetBoneTrans)
        //         {
        //             return i;
        //         }
        //     }
        //
        //     return -1;
        // }
        
        private static (int,Transform) GetBoneIndex(List<Transform> targetAvatarBones, string targetBoneName)
        {
            for (int i = 0; i < targetAvatarBones.Count; i++)
            {
                if (targetAvatarBones[i].name == targetBoneName)
                {
                    return (i,targetAvatarBones[i]);
                }
            }

            return (-1,null);
        }

    }
}