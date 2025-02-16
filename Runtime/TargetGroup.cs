using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace sui4.MaterialPropertyBaker
{
    [AddComponentMenu("MaterialPropertyBaker/MPB TargetGroup")]
    public class TargetGroup : MonoBehaviour
    {
        [SerializeField] private GameObject _target;

        [SerializeField]
        private SerializedDictionary<Renderer, MaterialTargetInfoSDictWrapper> _rendererMatTargetInfoWrapperSDict =
            new();

        [SerializeField] private List<Renderer> _renderers = new();

        private MaterialPropertyBlock _mpb; // to reduce GCAlloc
        private static Dictionary<int, float> _usedPropWeightDict;

        public Dictionary<Renderer, MaterialTargetInfoSDictWrapper> RendererMatTargetInfoWrapperDict =>
            _rendererMatTargetInfoWrapperSDict.Dictionary;

        public SerializedDictionary<Renderer, MaterialTargetInfoSDictWrapper> RendererMatTargetInfoWrapperSDict =>
            _rendererMatTargetInfoWrapperSDict;

        public List<Renderer> Renderers => _renderers;
        public List<string> Warnings { get; } = new();

        public Dictionary<Material, MaterialProps> DefaultMaterialPropsDict { get; } = new();

        private void OnEnable()
        {
            OnValidate();
            ResetPropertyBlock();
        }

        public void OnValidate()
        {
            Warnings.Clear();
            if (_target == null)
            {
                _target = this.gameObject;
            }

            SyncRenderer();
            SyncMaterial();
            RetrieveInitialProps();
        }

        private void RetrieveInitialProps()
        {
            DefaultMaterialPropsDict.Clear();
            foreach (Renderer ren in Renderers)
            {
                if (ren == null) continue;
                MaterialTargetInfoSDictWrapper wrapper = RendererMatTargetInfoWrapperDict[ren];
                foreach (Material mat in wrapper.MatTargetInfoDict.Keys)
                {
                    var defaultProps = new MaterialProps(mat);
                    DefaultMaterialPropsDict.TryAdd(mat, defaultProps);
                }
            }
        }

        private void SyncMaterial()
        {
            foreach (Renderer ren in Renderers)
            {
                if (ren == null) continue;
                RendererMatTargetInfoWrapperDict.TryAdd(ren, new MaterialTargetInfoSDictWrapper());

                MaterialTargetInfoSDictWrapper matTargetInfoSDictWrapper = RendererMatTargetInfoWrapperDict[ren];
                // 削除されたmaterialを取り除く
                var matKeysToRemove = new List<Material>();
                foreach (Material mat in matTargetInfoSDictWrapper.MatTargetInfoDict.Keys)
                    if (!ren.sharedMaterials.Contains(mat))
                        matKeysToRemove.Add(mat);

                foreach (Material mat in matKeysToRemove)
                    matTargetInfoSDictWrapper.MatTargetInfoDict.Remove(mat);

                // 追加されたmaterialを追加する
                foreach (Material mat in ren.sharedMaterials)
                {
                    if (matTargetInfoSDictWrapper.MatTargetInfoDict.ContainsKey(mat)) continue;
                    var targetInfo = new TargetInfo
                    {
                        ID = mat.name,
                        Material = mat
                    };
                    matTargetInfoSDictWrapper.MatTargetInfoDict.Add(mat, targetInfo);
                }
            }
        }

        private void SyncRenderer()
        {
            if (_target == null) return;

            List<Renderer> renderers = new();
            _target.GetComponentsInChildren(true, renderers);
            // mesh renderer, skinned mesh renderer以外を取り除く
            renderers = renderers.Where(ren =>
                ren is MeshRenderer or SkinnedMeshRenderer).ToList();
            var renderersToRemove = new List<Renderer>();
            foreach (Renderer ren in Renderers)
            {
                if (renderers.Contains(ren)) continue;
                renderersToRemove.Add(ren);
            }

            foreach (Renderer ren in renderersToRemove)
            {
                Renderers.Remove(ren);
                RendererMatTargetInfoWrapperDict.Remove(ren);
            }

            var renderersToAdd = new List<Renderer>();
            foreach (Renderer ren in renderers)
            {
                if (Renderers.Contains(ren)) continue;
                renderersToAdd.Add(ren);
            }

            foreach (Renderer ren in renderersToAdd)
            {
                Renderers.Add(ren);
                RendererMatTargetInfoWrapperDict.TryAdd(ren, new MaterialTargetInfoSDictWrapper());
            }
        }

        // validate shader name: 同じIDを持つmaterialのshaderが同じかどうか
        private void ValidateShader()
        {
        }

        public void SetPropertyBlock(Dictionary<MpbProfile, float> profileWeightDict)
        {
            // merge global profile
            Dictionary<MpbProfile, Dictionary<string, MaterialProps>> mergedPropsDictDict = new();
            foreach ((MpbProfile profile, float _) in profileWeightDict)
            {
                MergeGlobalProps(profile, out Dictionary<string, MaterialProps> mergedPropsDict);
                mergedPropsDictDict[profile] = mergedPropsDict;
            }

            foreach (Renderer ren in Renderers)
            {
                MaterialTargetInfoSDictWrapper wrapper = RendererMatTargetInfoWrapperDict[ren];
                for (var mi = 0; mi < ren.sharedMaterials.Length; mi++)
                {
                    Material mat = ren.sharedMaterials[mi];
                    TargetInfo targetInfo = wrapper.MatTargetInfoDict[mat];
                    MaterialProps defaultProps = DefaultMaterialPropsDict[mat];
                    // ren.GetPropertyBlock(_mpb, mi); // 初期化時にsetしてるため、ここで例外は発生しないはず
                    _mpb = new MaterialPropertyBlock();
                    Dictionary<int, float> usedPropertyWeightDict = new();
                    foreach ((MpbProfile profile, float weight) in profileWeightDict)
                    {
                        if (mergedPropsDictDict[profile].TryGetValue(targetInfo.ID, out MaterialProps props))
                        {
                            SetPropertyBlock(props, weight, defaultProps, usedPropertyWeightDict, _mpb);
                        }
                    }

                    ren.SetPropertyBlock(_mpb, mi);
                }
            }
        }

        // materialから取得したdefault propertyに存在しないpropertyは無視する
        private static void SetPropertyBlock(MaterialProps targetProps, float weight, MaterialProps defaultProps,
            Dictionary<int, float> usedPropWeightDict, MaterialPropertyBlock mpb)
        {
            foreach (MaterialProp<Color> color in targetProps.Colors)
            {
                MaterialProp<Color> defaultProp = defaultProps.Colors.Find(c => c.ID == color.ID);
                if (defaultProp == null) continue;
                Color current = defaultProp.Value;
                if (usedPropWeightDict.TryAdd(defaultProp.ID, weight) == false)
                    current = mpb.GetColor(defaultProp.ID); //already set

                Color diff = color.Value - defaultProp.Value;
                mpb.SetColor(defaultProp.ID, current + diff * weight);
            }

            foreach (MaterialProp<float> f in targetProps.Floats)
            {
                MaterialProp<float> prop = defaultProps.Floats.Find(c => c.ID == f.ID);
                if (prop == null) continue;
                float current = prop.Value;
                if (usedPropWeightDict.TryAdd(prop.ID, weight) == false)
                    current = mpb.GetFloat(prop.ID); // already set

                float diff = f.Value - prop.Value;
                mpb.SetFloat(prop.ID, current + diff * weight);
            }

            foreach (MaterialProp<int> i in targetProps.Ints)
            {
                MaterialProp<int> prop = defaultProps.Ints.Find(c => c.ID == i.ID);
                if (prop == null) continue;
                if (usedPropWeightDict.TryGetValue(prop.ID, out var storedWeight) && weight > storedWeight)
                {
                    mpb.SetInt(prop.ID, i.Value);
                    usedPropWeightDict[prop.ID] = weight;
                }
                else
                {
                    mpb.SetInt(prop.ID, i.Value);
                    usedPropWeightDict.Add(prop.ID, weight);
                }
            }
        }

        // 個別に設定された値を優先する
        private static void MergeGlobalProps(MpbProfile profile, out Dictionary<string, MaterialProps> mergedPropsDict)
        {
            mergedPropsDict = new Dictionary<string, MaterialProps>();
            foreach ((string id, MaterialProps props) in profile.IdMaterialPropsDict)
            {
                MaterialProps mergedProps = MergeMaterialProps(new MaterialProps[2] { profile.GlobalProps, props });
                mergedPropsDict[id] = mergedProps;
            }
        }


        // layerが上(indexが大きい)のを優先する
        private static MaterialProps MergeMaterialProps(in IReadOnlyList<MaterialProps> layeredProps)
        {
            MaterialProps mergedProps = new();
            Dictionary<int, MaterialProp<Color>> idColorDict = new();
            Dictionary<int, MaterialProp<float>> idFloatDict = new();
            Dictionary<int, MaterialProp<int>> idIntDict = new();
            for (int li = 0; li < layeredProps.Count; li++)
            {
                MaterialProps target = layeredProps[li];
                foreach (MaterialProp<Color> colorProp in target.Colors)
                    idColorDict[colorProp.ID] = colorProp;

                foreach (MaterialProp<float> floatProp in target.Floats)
                    idFloatDict[floatProp.ID] = floatProp;

                foreach (MaterialProp<int> intProp in target.Ints)
                    idIntDict[intProp.ID] = intProp;
            }

            foreach ((int _, MaterialProp<Color> colorProp) in idColorDict)
                mergedProps.Colors.Add(colorProp);

            foreach ((int _, MaterialProp<float> floatProp) in idFloatDict)
                mergedProps.Floats.Add(floatProp);

            foreach ((int _, MaterialProp<int> intProp) in idIntDict)
                mergedProps.Ints.Add(intProp);
            return mergedProps;
        }

        public void ResetPropertyBlock()
        {
            _mpb = new MaterialPropertyBlock();
            foreach (Renderer ren in Renderers)
                for (var mi = 0; mi < ren.sharedMaterials.Length; mi++)
                    ren.SetPropertyBlock(_mpb, mi);
        }

        public void ResetToDefault()
        {
            ResetPropertyBlock();
        }

#if UNITY_EDITOR
        [ContextMenu("Create MPB Profile Asset")]
        public void CreateMpbProfileAsset()
        {
            var asset = ScriptableObject.CreateInstance<MpbProfile>();
            Dictionary<Shader, int> matNumDict = new();
            foreach (Renderer ren in Renderers)
            {
                MaterialTargetInfoSDictWrapper wrapper = RendererMatTargetInfoWrapperDict[ren];
                for (var mi = 0; mi < ren.sharedMaterials.Length; mi++)
                {
                    Material mat = ren.sharedMaterials[mi];
                    if (matNumDict.ContainsKey(mat.shader))
                        matNumDict[mat.shader] += 1;
                    else
                        matNumDict[mat.shader] = 1;

                    TargetInfo targetInfo = wrapper.MatTargetInfoDict[mat];
                    if (!asset.IdMaterialPropsDict.ContainsKey(targetInfo.ID))
                    {
                        var matProps = new MaterialProps(mat, false);
                        matProps.ID = targetInfo.ID;
                        asset.MaterialPropsList.Add(matProps);
                        asset.IdMaterialPropsDict.Add(targetInfo.ID, matProps);
                    }
                }
            }

            // 最も数が多いshaderをglobalに設定
            int maxNum = 0;
            foreach ((Shader shader, int num) in matNumDict)
            {
                if (maxNum < num)
                {
                    asset.GlobalProps.Shader = shader;
                    maxNum = num;
                }
            }

            var defaultName = $"{name}_profile";
            Utils.CreateAsset(asset, defaultName, "Create MPB Profile", "");
        }
#endif
    }

    [Serializable]
    public class MaterialTargetInfoSDictWrapper
    {
        [SerializeField] private SerializedDictionary<Material, TargetInfo> _matTargetInfoSDict = new();
        public Dictionary<Material, TargetInfo> MatTargetInfoDict => _matTargetInfoSDict.Dictionary;
    }

    [Serializable]
    public class TargetInfo
    {
        [SerializeField] private string _id;
        [SerializeField] private Material _material;

        public string ID
        {
            get => _id;
            set => _id = value;
        }

        public Material Material
        {
            get => _material;
            set => _material = value;
        }
    }
}