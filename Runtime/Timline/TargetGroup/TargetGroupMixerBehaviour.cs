using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;

namespace sui4.MaterialPropertyBaker.Timeline
{

    public class TargetGroupMixerBehaviour : PlayableBehaviour
    {
        private readonly Dictionary<MpbProfile, float> _profileWeightDict = new();

        public TargetGroup BindingTargetGroup;
        private Dictionary<int, bool> _isWarningLogged = new();
        

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            BindingTargetGroup = playerData as TargetGroup;
            if (BindingTargetGroup == null)
                return;

            var inputCount = playable.GetInputCount();
            float totalWeight = 0;

            _profileWeightDict.Clear();

            for (var i = 0; i < inputCount; i++)
            {
                var inputWeight = playable.GetInputWeight(i);
                // 各paramの重み付き和
                if (inputWeight > 0)
                {
                    var sp = (ScriptPlayable<TargetGroupBehaviour>)playable.GetInput(i);
                    var clip = sp.GetBehaviour().Clip;
                    if (clip.MpbProfile == null)
                    {
                        if (inputWeight < 1 && !_isWarningLogged[i])
                        {
                            Debug.LogWarning(
                                $"{clip.name} has no MPBProfile.\n This can lead to unexpected behavior when blending.");
                            _isWarningLogged[i] = true;
                        }

                        continue;
                    }

                    totalWeight += inputWeight;
                    if (_profileWeightDict.TryGetValue(clip.MpbProfile, out var weight))
                    {
                        _profileWeightDict[clip.MpbProfile] = weight + inputWeight;
                    }
                    else
                    {
                        _profileWeightDict.Add(clip.MpbProfile, inputWeight);
                    }
                    
                }
            }

            if (totalWeight > 0f)
            {
                BindingTargetGroup.SetPropertyBlock(_profileWeightDict);
            }
            else
            {
                BindingTargetGroup.ResetToDefault();
            }
        }

        public override void OnGraphStart(Playable playable)
        {
            var inputCount = playable.GetInputCount();
            _isWarningLogged.Clear();
            if (BindingTargetGroup != null)
            {
                BindingTargetGroup.ResetPropertyBlock();
            }

            for (var i = 0; i < inputCount; i++)
            {
                _isWarningLogged.Add(i, false);
                var sp = (ScriptPlayable<TargetGroupBehaviour>)playable.GetInput(i);
                var clip = sp.GetBehaviour().Clip;
                if (clip.MpbProfile == null) continue;
                
                
                foreach (var matProps in clip.MpbProfile.MaterialPropsList)
                {
                    if(matProps == null) continue;
                    matProps.UpdateShaderID();
                }
            }

            base.OnGraphStart(playable);
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            if (BindingTargetGroup == null)
                return;

            BindingTargetGroup.ResetToDefault();
        }
    }
}