using UnityEngine;
using UnityEngine.UI;

// A narrow-bottom, wide-top meter drawn directly as UI geometry.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ThrowChargeBar : MaskableGraphic
{
    [SerializeField, Range(0f, 1f)] private float fillAmount;
    [SerializeField] private Text speedLabel;

    private float minimumSpeed = 9f;
    private float maximumSpeed = 25f;

    public float FillAmount
    {
        get => fillAmount;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(fillAmount, clamped)) return;
            fillAmount = clamped;
            SetVerticesDirty();
            UpdateSpeedLabel();
        }
    }

    public void SetSpeedRange(float minimum, float maximum)
    {
        minimumSpeed = minimum;
        maximumSpeed = maximum;
        UpdateSpeedLabel();
    }

    private void UpdateSpeedLabel()
    {
        if (speedLabel != null)
            speedLabel.text = Mathf.Lerp(minimumSpeed, maximumSpeed, fillAmount).ToString("0.0") + " m/s";
    }

protected override void OnPopulateMesh(VertexHelper vh)
{
    vh.Clear();
    Rect rect = rectTransform.rect;
    float bottom = rect.yMin;
    float top = rect.yMax;
    float height = rect.height;

    DrawBand(vh, bottom, top, 0f, new Color32(0, 0, 0, 255), rect);
    DrawBand(vh, bottom + 6f, top - 6f, 6f, new Color32(224, 216, 158, 255), rect);

    if (fillAmount > 0f)
    {
        float fillBottom = bottom + 9f;
        float fillTop = fillBottom + (height - 18f) * fillAmount;
        DrawGradientFill(vh, fillBottom, fillTop, 9f, rect);
    }
}

    private static float HalfWidth(float y, Rect rect)
    {
        float t = Mathf.InverseLerp(rect.yMin, rect.yMax, y);
        return rect.width * (0.11f + 0.34f * Mathf.Pow(t, 3.2f));
    }

    private static void DrawBand(VertexHelper vh, float bottom, float top, float inset, Color32 color, Rect rect)
    {
        const int steps = 36;
        if (top <= bottom) return;

        for (int i = 0; i < steps; i++)
        {
            float y0 = Mathf.Lerp(bottom, top, i / (float)steps);
            float y1 = Mathf.Lerp(bottom, top, (i + 1) / (float)steps);
            float w0 = Mathf.Max(0f, HalfWidth(y0, rect) - inset);
            float w1 = Mathf.Max(0f, HalfWidth(y1, rect) - inset);
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(-w0, y0), color, Vector2.zero);
            vh.AddVert(new Vector3(w0, y0), color, Vector2.zero);
            vh.AddVert(new Vector3(w1, y1), color, Vector2.zero);
            vh.AddVert(new Vector3(-w1, y1), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }


private static void DrawGradientFill(VertexHelper vh, float bottom, float top, float inset, Rect rect)
{
    const int steps = 36;
    if (top <= bottom) return;

    for (int i = 0; i < steps; i++)
    {
        float y0 = Mathf.Lerp(bottom, top, i / (float)steps);
        float y1 = Mathf.Lerp(bottom, top, (i + 1) / (float)steps);
        float w0 = Mathf.Max(0f, HalfWidth(y0, rect) - inset);
        float w1 = Mathf.Max(0f, HalfWidth(y1, rect) - inset);
        Color32 color = EvaluateChargeColor(Mathf.InverseLerp(rect.yMin, rect.yMax, (y0 + y1) * 0.5f));
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(-w0, y0), color, Vector2.zero);
        vh.AddVert(new Vector3(w0, y0), color, Vector2.zero);
        vh.AddVert(new Vector3(w1, y1), color, Vector2.zero);
        vh.AddVert(new Vector3(-w1, y1), color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}

private static Color32 EvaluateChargeColor(float amount)
{
    Color green = new Color(0.15f, 0.95f, 0.18f, 1f);
    Color yellow = new Color(1f, 0.9f, 0.08f, 1f);
    Color orange = new Color(1f, 0.48f, 0.02f, 1f);
    Color red = new Color(1f, 0.05f, 0.02f, 1f);

    if (amount < 0.5f)
        return Color.Lerp(green, yellow, amount / 0.5f);
    if (amount < 0.78f)
        return Color.Lerp(yellow, orange, (amount - 0.5f) / 0.28f);
    return Color.Lerp(orange, red, (amount - 0.78f) / 0.22f);
}
}
