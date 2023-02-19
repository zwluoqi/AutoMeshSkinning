using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Content.Scritps
{
    public static class Util
    {
        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
        
        public static T AddMissingComponent<T>(this GameObject _gameObject) where T : Component
        {
            var t = _gameObject.GetComponent<T>();
            if (!t)
            {
              t = _gameObject.AddComponent<T>();
            }

            return t;
            
            
        }

        public static void ResetTrans(Transform rootTransform)
        {
            rootTransform.localPosition =Vector3.zero;
            rootTransform.localRotation = Quaternion.identity;
            rootTransform.localScale = Vector3.one;
        }

        public static string GetTimeDesc(float maxVideoTime)
        {
            var hour = maxVideoTime / 3600;
            var min = (maxVideoTime % 3600) / 60;
            var second = maxVideoTime % 60;
            if (hour > 0)
            {
                return $"{hour:00}:{min:00}:{second:00}";
            }else
            {
                return $"{min:00}:{second:00}";
            }
        }

        public static List<Transform> GetTransforms(Transform t)
        {
            List<Transform> singleParentFamily = new List<Transform>() { t };

            foreach (Transform childT in t)
            {
                singleParentFamily.AddRange(GetTransforms(childT));
            }

            return singleParentFamily;
        }

       
        static void makeTransformDictionary(Transform rootBone, Dictionary<string, Transform> dictionary)
        {
            if (dictionary.ContainsKey(rootBone.name))
            {
                return;
            }

            dictionary.Add(rootBone.name, rootBone);
            foreach (Transform childT in rootBone)
            {
                makeTransformDictionary(childT, dictionary);
            }
        }
        
        public static void EnforceTPose(Animator animator, bool aPose = false)
        {
            if (animator == null)
            {
                UnityEngine.Debug.Log("EnforceInitialPose");
                UnityEngine.Debug.Log("Animatorがnullです");
                return;
            }

            const int APoseDegree = 30;

            var trans = animator.transform;
            Vector3 position = trans.position;
            Quaternion rotation = trans.rotation;
            trans.position = Vector3.zero;
            trans.rotation = Quaternion.identity;

            Dictionary<string, Transform> transformDictionary = new Dictionary<string, Transform>();
            makeTransformDictionary(trans, transformDictionary);
            
            var skeletons = animator.avatar.humanDescription.skeleton;
            int count = skeletons.Length;
            for (int i = 0; i < count; i++)
            {
                if (!transformDictionary.ContainsKey(skeletons[i].name))
                {
                    continue;
                }

                transformDictionary[skeletons[i].name].localPosition
                    = skeletons[i].position;
                transformDictionary[skeletons[i].name].localRotation
                    = skeletons[i].rotation;
            }

            trans.position = position;
            trans.rotation = rotation;

            if (aPose && animator.isHuman)
            {
                Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if (leftUpperArm == null || rightUpperArm == null)
                {
                    return;
                }
            
                leftUpperArm.Rotate(animator.transform.forward, APoseDegree, Space.World);
                rightUpperArm.Rotate(animator.transform.forward, -APoseDegree, Space.World);
            }
        }


        public static Vector4 ToVector4One(this Vector3 source)
        {
            return new Vector4(source.x, source.y, source.z, 1);
        }

        public static string GetFileSize(long fileInfoLength)
        {
            if (fileInfoLength > 1000*1000)
            {
                return (fileInfoLength * 0.001 * 0.001).ToString("F1")+"MB";
            }
            else if(fileInfoLength>1000)
            {
                return (fileInfoLength * 0.001 ).ToString("F1")+"KB";
            }
            else
            {
                return "<1KB";
            }
        }

        public static async Task CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            // 判断目标目录是否存在，如果不存在则创建
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // 获取源目录中的所有文件和子目录
            DirectoryInfo sourceDirectoryInfo = new DirectoryInfo(sourceDirectory);
            FileInfo[] files = sourceDirectoryInfo.GetFiles();
            DirectoryInfo[] subDirectories = sourceDirectoryInfo.GetDirectories();

            // 拷贝文件到目标目录
            foreach (FileInfo file in files)
            {
                string destinationFilePath = Path.Combine(destinationDirectory, file.Name);
                await Task.Run(() =>
                {
                    file.CopyTo(destinationFilePath, true);
                });
                Debug.LogWarning($"copy from {file.FullName} to {destinationFilePath}");
            }

            // 递归地拷贝子目录到目标目录
            foreach (DirectoryInfo subDirectory in subDirectories)
            {
                string destinationSubDirectoryPath = Path.Combine(destinationDirectory, subDirectory.Name);
                await CopyDirectory(subDirectory.FullName, destinationSubDirectoryPath);
            }
        }

        public static List<GameObject> CreateMeshRendersBySMR(GameObject getResObject,GameObject instanceMeshPoolRoot)
        {
            List<GameObject> gos = new List<GameObject>();
            var skinnedMeshRenderers = getResObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                var go = new GameObject(skinnedMeshRenderer.name);
                var meshFilter = go.AddComponent<MeshFilter>();
                var meshRender = go.AddComponent<MeshRenderer>();
                meshFilter.sharedMesh = skinnedMeshRenderer.sharedMesh;
                meshRender.sharedMaterials = skinnedMeshRenderer.sharedMaterials;
                go.transform.SetParent(instanceMeshPoolRoot.transform);
                Util.ResetTrans(go.transform);
                gos.Add(go);
            }

            return gos;
        }

        public static List<GameObject> CreateMeshRendersByMeshs(Material[] materials,Mesh[] meshes, Transform instanceMeshPoolRoot)
        {
            List<GameObject> gos = new List<GameObject>();
            for(int i=0;i<materials.Length;i++)
            {
                var go = CreateMeshRendersByMesh(materials[i], meshes[i], instanceMeshPoolRoot);
                gos.Add(go);
            }

            return gos;
        }
        
        public static GameObject CreateMeshRendersByMesh(Material material,Mesh meshe, Transform instanceMeshPoolRoot)
        {
            var root = new GameObject(material.name+"_root");
            root.transform.SetParent(root.transform);
            Util.ResetTrans(root.transform);
            
            var parent = new GameObject(material.name+"_collider");
            var meshCollider = parent.AddComponent<BoxCollider>();
            meshCollider.isTrigger = true;
            meshCollider.size= meshe.bounds.size;
            parent.transform.SetParent(root.transform);
            Util.ResetTrans(parent.transform);
            parent.transform.localPosition = meshe.bounds.center;

            
            var go = new GameObject(material.name);
            var meshFilter = go.AddComponent<MeshFilter>();
            var meshRender = go.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = meshe;
            meshRender.sharedMaterial = material;
            go.transform.SetParent(parent.transform);
            Util.ResetTrans(go.transform);
            go.transform.localPosition = -meshe.bounds.center;
            return root;
        }
        
        
        public static SkinnedMeshRenderer CreateSMRByMesh(Material material,Mesh mesh, Transform parent,Transform rootBone,Transform[] bones)
        {
            mesh.name = material.name;
            var go = new GameObject(mesh.name);
            var skinnedMeshRenderer = go.AddComponent<SkinnedMeshRenderer>();

            skinnedMeshRenderer.sharedMaterial = material;
            skinnedMeshRenderer.bones = bones;
            skinnedMeshRenderer.quality = SkinQuality.Bone4;
            skinnedMeshRenderer.rootBone = rootBone;
            skinnedMeshRenderer.sharedMesh = mesh;
            
            go.transform.SetParent(parent);
            Util.ResetTrans(go.transform);
            return skinnedMeshRenderer;
        }

        public static void BindMeshCollider(Transform transform)
        {
            var skinnedMeshRenderers = transform.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                var meshCollider = skinnedMeshRenderer.gameObject.AddMissingComponent<MeshCollider>();
                meshCollider.sharedMesh = skinnedMeshRenderer.sharedMesh;
            }
        }
        public static void RemoveMeshCollider(Transform transform)
        {
            var skinnedMeshRenderers = transform.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                var meshCollider = skinnedMeshRenderer.gameObject.GetComponent<MeshCollider>();
                Object.Destroy(meshCollider);
            }
        }

        public static GameObject CloneGameObjectOnlyWithSRM(List<SkinnedMeshRenderer> sources)
        {
            var root  = sources[0].transform.parent;
            var clone = GameObject.Instantiate(root.gameObject);
            clone.SetActive(true);
            var smrs = clone.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in smrs)
            {
                if (sources.Any(obj => obj.name == smr.name))
                {
                    continue;
                }
                Object.DestroyImmediate(smr.gameObject);
            }

            return clone.gameObject;
        }
    }
}