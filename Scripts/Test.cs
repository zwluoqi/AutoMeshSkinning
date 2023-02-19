using System;
using UnityEngine;

namespace Content.Scritps
{
    public class Test:MonoBehaviour
    {
        
        
        public Animator target;
        public SkinnedMeshRenderer source;

        public bool combine;


        private void Update()
        {
            if (combine)
            {
                combine = false;

                
                CustomMeshUtil.CustomAvatar sourceAvatar = new CustomMeshUtil.CustomAvatar()
                {
                    animatar = source.GetComponentInParent<Animator>(),
                    rootBone = source.rootBone
                };
                
                CustomMeshUtil.CustomAvatar targetAvatar = new CustomMeshUtil.CustomAvatar()
                {
                    animatar = target.GetComponent<Animator>(),
                    rootBone = target.transform
                };

                Util.ResetTrans(sourceAvatar.animatar.transform);
                Util.ResetTrans(targetAvatar.animatar.transform);
                Util.EnforceTPose(sourceAvatar.animatar);
                Util.EnforceTPose(targetAvatar.animatar);
                CustomMeshUtil.BindSMRToTargetAvatar(source, sourceAvatar, targetAvatar);
            }
        }
    }
}