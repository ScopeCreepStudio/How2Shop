using UnityEngine;
using UnityEngine.UI;

namespace How2Shop.Chaos.UI
{
    /// <summary>A slightly lopsided, black-outlined star drawn as ordinary UI triangles.</summary>
    [AddComponentMenu("")]
    public sealed class ChaosStarGraphic : MaskableGraphic
    {
        private bool filled;
        private bool flashing;
        private float seed;

        public void SetSeed(float value)
        {
            seed = value;
            SetVerticesDirty();
        }

        public void SetFilled(bool value)
        {
            if (filled == value)
                return;
            filled = value;
            SetVerticesDirty();
        }

        public void SetFlashing(bool value)
        {
            if (flashing == value)
                return;
            flashing = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.48f;
            Vector2 center = rect.center;
            Vector2[] points = BuildStarPoints(center, radius, seed);
            AddFan(vertexHelper, points, Color.black);

            Color inner = flashing
                ? new Color32(255, 255, 255, 255)
                : filled
                    ? new Color32(255, 239, 0, 255)
                    : new Color32(148, 142, 116, 255);
            Vector2[] innerPoints = BuildStarPoints(center, radius * 0.78f, seed + 0.37f);
            AddFan(vertexHelper, innerPoints, inner);
        }

        private static Vector2[] BuildStarPoints(Vector2 center, float radius, float wobbleSeed)
        {
            var points = new Vector2[10];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                bool outer = (i & 1) == 0;
                float baseRadius = outer ? radius : radius * 0.47f;
                float wobble = 1f + Mathf.Sin(i * 7.13f + wobbleSeed) * (outer ? 0.075f : 0.13f);
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * baseRadius * wobble;
            }
            return points;
        }

        private static void AddFan(VertexHelper vh, Vector2[] points, Color color)
        {
            int centerIndex = vh.currentVertCount;
            Vector2 center = Vector2.zero;
            for (int i = 0; i < points.Length; i++)
                center += points[i];
            center /= points.Length;
            vh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < points.Length; i++)
                vh.AddVert(points[i], color, Vector2.zero);
            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;
                vh.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 1 + next);
            }
        }
    }
}
