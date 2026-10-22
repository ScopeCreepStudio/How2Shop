using UnityEngine;

// Keeps dynamic font atlases bound to the depth-tested, single-sided sign material.
[ExecuteAlways, RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
public sealed class SupermarketSignFont : MonoBehaviour
{
    private TextMesh text;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock properties;

    private void OnEnable()
    {
        text = GetComponent<TextMesh>();
        meshRenderer = GetComponent<MeshRenderer>();
        properties = new MaterialPropertyBlock();
        Font.textureRebuilt += OnTextureRebuilt;
        if (text.font) text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
        Apply();
    }

    private void OnDisable() => Font.textureRebuilt -= OnTextureRebuilt;
    private void OnTextureRebuilt(Font font) { if (text && text.font == font) Apply(); }
    private void Apply()
    {
        if (!text || !text.font || !meshRenderer) return;
        meshRenderer.GetPropertyBlock(properties);
        properties.SetTexture("_MainTex", text.font.material.mainTexture);
        meshRenderer.SetPropertyBlock(properties);
    }
}
