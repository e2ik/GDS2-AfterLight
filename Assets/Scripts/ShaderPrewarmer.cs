using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class ShaderPrewarmer : MonoBehaviour
{
    [Tooltip("Assign exactly the materials you want precompiled.")]
    [SerializeField] private List<Material> materialsToWarm = new List<Material>();

    private ShaderVariantCollection runtimeCollection;

    private void Awake()
    {
        runtimeCollection = new ShaderVariantCollection();

        foreach (var mat in materialsToWarm)
        {
            if (mat == null || mat.shader == null) continue;

            var variant = new ShaderVariantCollection.ShaderVariant
            {
                shader = mat.shader,
                passType = PassType.ScriptableRenderPipeline,
                keywords = mat.shaderKeywords
            };

            runtimeCollection.Add(variant);
        }

        runtimeCollection.WarmUp();
    }
}