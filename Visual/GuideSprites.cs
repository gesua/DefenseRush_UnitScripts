using UnityEngine;

namespace Defense.Units
{
    /// <summary>
    /// 선택/드래그 안내용 스프라이트를 코드로 만든다. (이미지 에셋이 필요 없다)
    /// 만든 스프라이트는 쓰는 쪽에서 DestroySprite로 정리한다.
    /// 색은 SpriteRenderer.color로 입히므로 스프라이트는 전부 흰색이다.
    /// </summary>
    public static class GuideSprites
    {
        // 점선 한 주기(점 + 간격)의 월드 길이
        const float DASH_PERIOD = 0.6f;

        /// <summary>점선의 굵기(월드 단위). 점선 스프라이트의 세로 크기와 같다. (16px / (64px / DASH_PERIOD))</summary>
        public const float DASH_HEIGHT = 0.15f;

        // 지름이 정확히 1유닛인 원(옅은 채움 + 테두리). PPU를 텍스처 크기와 같게 만들어서 1유닛이 된다.
        public static Sprite CreateCircle(int size = 256)
        {
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                    float edge = Mathf.Clamp01((1f - d) * radius);  // 경계 1픽셀 부드럽게
                    float alpha = ((d >= 0.96f) ? 0.8f : 0.2f) * edge; // 바깥 4%는 테두리
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return Build(size, size, pixels, size);
        }

        /// <summary>유닛 발밑 표시용 원. 지름이 정확히 1유닛이고, CreateCircle보다 채움이 진하다.</summary>
        public static Sprite CreateDisc(int size = 128)
        {
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                    float edge = Mathf.Clamp01((1f - d) * radius);  // 경계 1픽셀 부드럽게
                    float alpha = ((d >= 0.88f) ? 1f : 0.55f) * edge; // 바깥 12%는 진한 테두리
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return Build(size, size, pixels, size);
        }

        /// <summary>한 변이 1유닛인 칸 모양. 옅은 채움 + 진한 테두리(2px).</summary>
        public static Sprite CreateTile(int size = 64)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 가장자리까지의 거리(픽셀)
                    float edge = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
                    float alpha = (edge < 2f) ? 0.9f : 0.35f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return Build(size, size, pixels, size);
        }

        /// <summary>지름이 1유닛인 고리. thickness는 반지름 대비 두께 비율.</summary>
        public static Sprite CreateRing(int size = 256, float thickness = 0.1f)
        {
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                    float outer = Mathf.Clamp01((1f - d) * radius);                 // 바깥 경계
                    float inner = Mathf.Clamp01((d - (1f - thickness)) * radius);   // 안쪽 경계
                    float alpha = Mathf.Min(outer, inner);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            return Build(size, size, pixels, size);
        }

        /// <summary>한 변이 1유닛인 정사각형의 네 모서리만 그린 괄호 모양. thickness/length는 한 변 대비 비율.</summary>
        public static Sprite CreateBrackets(int size = 128, float thickness = 0.07f, float length = 0.3f)
        {
            var on = new Color32(255, 255, 255, 255);
            var off = new Color32(255, 255, 255, 0);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float dx = Mathf.Min(u, 1f - u);   // 좌우 가장자리까지의 거리
                    float dy = Mathf.Min(v, 1f - v);   // 위아래 가장자리까지의 거리

                    bool onBracket = (dx < thickness && dy < length) || (dy < thickness && dx < length);
                    pixels[y * size + x] = (onBracket) ? on : off;
                }
            }

            return Build(size, size, pixels, size);
        }

        /// <summary>점선 한 주기(점 + 간격). SpriteRenderer를 Tiled로 늘려서 쓴다. 세로 크기는 DASH_HEIGHT.</summary>
        public static Sprite CreateDash()
        {
            const int WIDTH = 64;
            const int HEIGHT = 16;
            const int OPAQUE_WIDTH = 40; // 앞 40px은 점, 나머지는 간격

            var pixels = new Color32[WIDTH * HEIGHT];
            for (int y = 0; y < HEIGHT; y++)
            {
                for (int x = 0; x < WIDTH; x++)
                {
                    byte alpha = (x < OPAQUE_WIDTH) ? (byte)255 : (byte)0;
                    pixels[y * WIDTH + x] = new Color32(255, 255, 255, alpha);
                }
            }

            return Build(WIDTH, HEIGHT, pixels, WIDTH / DASH_PERIOD);
        }

        /// <summary>만든 스프라이트와 텍스처를 함께 정리한다.</summary>
        public static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;

            Object.Destroy(sprite.texture);
            Object.Destroy(sprite);
        }

        // Tiled로 늘려 쓰는 스프라이트도 있어서 메시는 항상 FullRect로 만든다.
        static Sprite Build(int width, int height, Color32[] pixels, float pixelsPerUnit)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}