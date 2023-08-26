using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace sui4.MaterialPropertyBaker
{
    [CustomEditor(typeof(MpbProfile))]
    public class MpbProfileEditor : Editor
    {
        private readonly List<bool> _propFoldoutList = new();
        private readonly List<bool> _colorsFoldoutList = new();
        private readonly List<bool> _floatsFoldoutList = new();
        private readonly List<bool> _intsFoldoutList = new();
        private bool _globalColorsFoldout = true;
        private bool _globalFloatsFoldout = true;
        private bool _globalIntsFoldout = true;

        private SerializedProperty _globalPropsProp;
        private SerializedProperty _materialPropsListProp;
        private SerializedProperty _materialPropsProp;
        private MpbProfile Target => (MpbProfile)target;

        private void OnEnable()
        {
            if (target == null) return;
            _materialPropsListProp = serializedObject.FindProperty("_materialPropsList");
            _globalPropsProp = serializedObject.FindProperty("_globalProps");
            Validate();
        }

        private string PropFoldoutKeyAt(string id) => $"{Target.name}_propFoldout_{id}";
        private string ColorsFoldoutKeyAt(string id) => $"{Target.name}_colorsFoldout_{id}";
        private string FloatsFoldoutKeyAt(string id) => $"{Target.name}_floatsFoldout_{id}";
        private string IntsFoldoutKeyAt(string id) => $"{Target.name}_intsFoldout_{id}";

        private void Validate()
        {
            for (var i = _propFoldoutList.Count; i < _materialPropsListProp.arraySize; i++)
            {
                var key = string.IsNullOrWhiteSpace(Target.MaterialPropsList[i].ID)
                    ? i.ToString()
                    : Target.MaterialPropsList[i].ID;
                _propFoldoutList.Add(SessionState.GetBool(PropFoldoutKeyAt(key), true));
                _colorsFoldoutList.Add(SessionState.GetBool(ColorsFoldoutKeyAt(key), true));
                _floatsFoldoutList.Add(SessionState.GetBool(FloatsFoldoutKeyAt(key), true));
                _intsFoldoutList.Add(SessionState.GetBool(IntsFoldoutKeyAt(key), true));
            }
        }

        public override void OnInspectorGUI()
        {
            if (target == null) return;
            serializedObject.Update();
            if (_materialPropsListProp.arraySize > _propFoldoutList.Count)
                Validate();

            EditorUtils.WarningGUI(Target.Warnings);
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                // global settings
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    GlobalPropertyGUI(_globalPropsProp);
                }

                using (new GUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Per Property Settings", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    // per id settings
                    for (var i = 0; i < _materialPropsListProp.arraySize; i++)
                    {
                        _materialPropsProp = _materialPropsListProp.GetArrayElementAtIndex(i);
                        string key, title;
                        if (string.IsNullOrWhiteSpace(Target.MaterialPropsList[i].ID))
                        {
                            key = i.ToString();
                            title = $"Material Property {i}";
                        }
                        else
                        {
                            key = title = Target.MaterialPropsList[i].ID;
                        }

                        _propFoldoutList[i] = EditorGUILayout.Foldout(_propFoldoutList[i], title);
                        SessionState.SetBool(PropFoldoutKeyAt(key), _propFoldoutList[i]);
                        if (_propFoldoutList[i])
                        {
                            EditorGUI.indentLevel++;
                            MaterialPropsGUI(_materialPropsProp, i);
                            EditorGUI.indentLevel--;
                        }
                    }

                    EditorGUI.indentLevel--;
                }

                if (change.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private void GlobalPropertyGUI(SerializedProperty globalPropsProp)
        {
            var shader = globalPropsProp.FindPropertyRelative("_shader");
            var colors = globalPropsProp.FindPropertyRelative("_colors");
            var floats = globalPropsProp.FindPropertyRelative("_floats");
            var ints = globalPropsProp.FindPropertyRelative("_ints");

            EditorGUILayout.LabelField("Global Properties", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(shader);

            var targetShader = shader.objectReferenceValue as Shader;
            // Colors
            _globalColorsFoldout = EditorGUILayout.Foldout(_globalColorsFoldout, "Colors");
            SessionState.SetBool(ColorsFoldoutKeyAt("global"), _globalColorsFoldout);
            if (_globalColorsFoldout)
            {
                EditorGUI.indentLevel++;
                PropsGUI(colors, Target.GlobalProps, ShaderPropertyType.Color, targetShader);
                EditorGUI.indentLevel--;
            }

            // Floats
            _globalFloatsFoldout = EditorGUILayout.Foldout(_globalFloatsFoldout, "Floats");
            SessionState.SetBool(FloatsFoldoutKeyAt("global"), _globalFloatsFoldout);
            if (_globalFloatsFoldout)
            {
                EditorGUI.indentLevel++;
                PropsGUI(floats, Target.GlobalProps, ShaderPropertyType.Float, targetShader);
                EditorGUI.indentLevel--;
            }

            // // Ints
            // _globalIntsFoldout = EditorGUILayout.Foldout(_globalIntsFoldout, "Ints");
            // SessionState.SetBool(IntsFoldoutKeyAt("global"), _globalIntsFoldout);
            // if (_globalIntsFoldout)
            // {
            //     EditorGUI.indentLevel++;
            //     PropsGUI(ints, Target.GlobalProps, ShaderPropertyType.Int);
            //     EditorGUI.indentLevel--;
            // }

            EditorGUI.indentLevel--;
        }

        private void MaterialPropsGUI(SerializedProperty materialPropsProp, int index)
        {
            var id = materialPropsProp.FindPropertyRelative("_id");
            var material = materialPropsProp.FindPropertyRelative("_material");
            var shader = materialPropsProp.FindPropertyRelative("_shader");
            var colors = materialPropsProp.FindPropertyRelative("_colors");
            var floats = materialPropsProp.FindPropertyRelative("_floats");
            var ints = materialPropsProp.FindPropertyRelative("_ints");

            EditorGUILayout.PropertyField(id, new GUIContent("ID"));
            EditorGUILayout.PropertyField(material);
            using (new EditorGUI.DisabledScope(material.objectReferenceValue != null))
            {
                EditorGUILayout.PropertyField(shader);
            }

            var targetShader = shader.objectReferenceValue as Shader;
            var key = string.IsNullOrWhiteSpace(id.stringValue) ? index.ToString() : id.stringValue;
            // Colors
            _colorsFoldoutList[index] = EditorGUILayout.Foldout(_colorsFoldoutList[index], "Colors");
            SessionState.SetBool(ColorsFoldoutKeyAt(key), _colorsFoldoutList[index]);
            if (_colorsFoldoutList[index])
            {
                EditorGUI.indentLevel++;
                PropsGUI(colors, Target.MaterialPropsList[index], ShaderPropertyType.Color, targetShader);
                EditorGUI.indentLevel--;
            }

            // Floats
            _floatsFoldoutList[index] = EditorGUILayout.Foldout(_floatsFoldoutList[index], "Floats");
            SessionState.SetBool(FloatsFoldoutKeyAt(key), _floatsFoldoutList[index]);
            if (_floatsFoldoutList[index])
            {
                EditorGUI.indentLevel++;
                PropsGUI(floats, Target.MaterialPropsList[index], ShaderPropertyType.Float, targetShader);
                EditorGUI.indentLevel--;
            }

            // // Ints
            // _intsFoldoutList[index] = EditorGUILayout.Foldout(_intsFoldoutList[index], "Ints");
            // SessionState.SetBool(IntsFoldoutKeyAt(key), _intsFoldoutList[index]);
            // if (_intsFoldoutList[index])
            // {
            //     EditorGUI.indentLevel++;
            //     PropsGUI(ints, Target.MaterialPropsList[index], ShaderPropertyType.Int);
            //     EditorGUI.indentLevel--;
            // }
        }

        private void PropsGUI(SerializedProperty propsList, MaterialProps matProps, ShaderPropertyType targetPropType,
            Shader targetShader)
        {
            if (propsList.arraySize == 0)
            {
                EditorGUILayout.LabelField("List is Empty");
            }

            for (int i = 0; i < propsList.arraySize; i++)
            {
                SerializedProperty prop = propsList.GetArrayElementAtIndex(i);
                var property = prop.FindPropertyRelative("_name");
                var valueProp = prop.FindPropertyRelative("_value");

                // attribute がある場合は、attribute を適用する
                var si = targetShader.FindPropertyIndex(property.stringValue);

                var shaderAttributes = targetShader.GetPropertyAttributes(si);
                ParseShaderAttribute(shaderAttributes, out var attribs, out var attribValues);
                // 最後の要素が適用されるため、逆順にする
                attribs.Reverse();
                attribValues.Reverse();

                var propType = targetShader.GetPropertyType(si);
                var label = Utils.UnderscoresToSpaces(property.stringValue);
                using (new GUILayout.HorizontalScope())
                {
                    switch (propType)
                    {
                        case ShaderPropertyType.Color:
                            valueProp.colorValue =
                                EditorGUILayout.ColorField(new GUIContent(label), valueProp.colorValue, true, true,
                                    true);
                            break;
                        case ShaderPropertyType.Float:
                            var controlCreated = false;
                            for (int ai = 0; ai < attribs.Count(); ai++)
                            {
                                if (attribs[ai] == "Toggle" || attribs[ai] == "MaterialToggle")
                                {
                                    var flag = valueProp.floatValue != 0;
                                    flag = EditorGUILayout.Toggle(new GUIContent(label), flag);
                                    valueProp.floatValue = flag ? 1 : 0;
                                    // キーワードの有効・無効の切り替えが必要？
                                    controlCreated = true;
                                }
                                else if (attribs[ai] == "Enum")
                                {
                                    // pop upの作成大変なので後で
                                    Debug.Log("Enum");
                                }
                                else if (attribs[ai] == "KeywordEnum")
                                {
                                    Debug.Log("KeywordEnum");
                                }
                            }

                            if (!controlCreated)
                            {
                                EditorGUILayout.PropertyField(valueProp, new GUIContent(label));
                            }

                            break;
                        case ShaderPropertyType.Range:
                            var min = ShaderUtil.GetRangeLimits(targetShader, si, 1);
                            var max = ShaderUtil.GetRangeLimits(targetShader, si, 2);
                            var created = false;
                            for (int ai = 0; ai < attribs.Count(); ai++)
                            {
                                if (attribs[ai] == "PowerSlider")
                                {
                                    // var power = 0f;
                                    // try
                                    //     power = float.Parse(attribValues[ai]);
                                    // catch
                                    //     continue;
                                    // CAUTION: PowerSliderはEditorGUILayoutにはなさそう？
                                    valueProp.floatValue =
                                        EditorGUILayout.Slider(label, valueProp.floatValue, min, max);
                                    created = true;
                                }
                                else if (attribs[ai] == "IntRange")
                                {
                                    valueProp.floatValue = EditorGUILayout.IntSlider(label, (int)valueProp.floatValue,
                                        (int)min, (int)max);
                                    created = true;
                                }
                            }

                            if (!created)
                            {
                                valueProp.floatValue = EditorGUILayout.Slider(label, valueProp.floatValue, min, max);
                            }

                            break;
                        case ShaderPropertyType.Int:
                            EditorGUILayout.PropertyField(valueProp, new GUIContent(label));
                            break;
                        case ShaderPropertyType.Vector:
                        case ShaderPropertyType.Texture:
                        default:
                            break;
                    }

                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        propsList.DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    ShowNewRecorderMenu(matProps, targetPropType);
                }
            }
        }

        private static void ParseShaderAttribute(IEnumerable<string> propAttributes, out List<string> attribs,
            out List<string> parameters)
        {
            attribs = new List<string>();
            parameters = new List<string>();
            foreach (var attr in propAttributes)
            {
                if (attr.IndexOf("Space", StringComparison.Ordinal) == 0) continue;
                if (attr.IndexOf("Header", StringComparison.Ordinal) == 0) continue;

                var matches = Regex.Matches(attr, @".*(?=\()"); //括弧の前を抽出
                if (matches.Count != 0)
                {
                    attribs.Add(matches[0].Value);
                    var paramMatches = Regex.Matches(attr, @"(?<=\().*(?=\))"); //括弧内を抽出
                    parameters.Add(paramMatches.Count != 0 ? paramMatches[0].Value : null);
                }
                else
                {
                    //括弧がない場合
                    attribs.Add(attr);
                    parameters.Add(null);
                }
            }
        }

        private static bool IsMatchShaderType(ShaderPropertyType s1, ShaderPropertyType s2)
        {
            if (s1 == s2) return true;
            switch (s1)
            {
                case ShaderPropertyType.Float when s2 == ShaderPropertyType.Range:
                case ShaderPropertyType.Range when s2 == ShaderPropertyType.Float:
                    return true;
                case ShaderPropertyType.Color:
                case ShaderPropertyType.Vector:
                case ShaderPropertyType.Texture:
                case ShaderPropertyType.Int:
                default:
                    return false;
            }
        }

        private void ShowNewRecorderMenu(MaterialProps matProps, ShaderPropertyType targetPropType)
        {
            var addPropertyMenu = new GenericMenu();
            var shader = matProps.Shader;
            for (var pi = 0; pi < shader.GetPropertyCount(); pi++)
            {
                var propName = shader.GetPropertyName(pi);
                var propType = shader.GetPropertyType(pi);
                if (!IsMatchShaderType(targetPropType, propType)) continue;
                
                // 隠しプロパティは追加しない
                var si = shader.FindPropertyIndex(propName);
                var flags = shader.GetPropertyFlags(si);
                if (flags == ShaderPropertyFlags.HideInInspector) continue;
                switch (propType)
                {
                    // すでに同じ名前のプロパティがある場合は追加しない
                    case ShaderPropertyType.Color when matProps.Colors.All(c => c.Name != propName):
                        AddPropertyToMenu(propName, addPropertyMenu, matProps, propType);
                        break;
                    case ShaderPropertyType.Float or ShaderPropertyType.Range
                        when matProps.Floats.All(f => f.Name != propName):
                        AddPropertyToMenu(propName, addPropertyMenu, matProps, propType);
                        break;
                    case ShaderPropertyType.Int when matProps.Ints.All(f => f.Name != propName):
                        AddPropertyToMenu(propName, addPropertyMenu, matProps, propType);
                        break;
                }
            }

            if (addPropertyMenu.GetItemCount() == 0)
            {
                addPropertyMenu.AddDisabledItem(new GUIContent("No Property to Add"));
            }

            addPropertyMenu.ShowAsContext();
        }

        private void AddPropertyToMenu(string propName, GenericMenu menu, MaterialProps props,
            ShaderPropertyType propType)
        {
            menu.AddItem(new GUIContent(propName), false, data => OnAddProperty((string)data, props, propType),
                propName);
        }

        private void OnAddProperty(string propName, MaterialProps props, ShaderPropertyType propType)
        {
            var material = props.Material;
            var shader = props.Shader;
            var propIndex = shader.FindPropertyIndex(propName);

            if (propType is ShaderPropertyType.Color)
            {
                var vCol = shader.GetPropertyDefaultVectorValue(propIndex);
                var defaultColor = material == null
                    ? new Color(vCol.x, vCol.y, vCol.z, vCol.w)
                    : material.GetColor(propName);
                var matProp = new MaterialProp<Color>(propName, defaultColor);
                props.Colors.Add(matProp);
            }
            else if (propType is ShaderPropertyType.Float or ShaderPropertyType.Range)
            {
                var defaultFloat = material == null
                    ? shader.GetPropertyDefaultFloatValue(propIndex)
                    : material.GetFloat(propName);
                var matProp = new MaterialProp<float>(propName, defaultFloat);
                props.Floats.Add(matProp);
            }
            else if (propType is ShaderPropertyType.Int)
            {
                var defaultInt = material == null
                    ? shader.GetPropertyDefaultIntValue(propIndex)
                    : material.GetInteger(propName);
                var matProp = new MaterialProp<int>(propName, defaultInt);
                props.Ints.Add(matProp);
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            serializedObject.Update();
        }
    }
}