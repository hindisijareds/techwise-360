using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TechWiseUITheme
{
    public static readonly Color32 Navy = new(9, 28, 64, 255);
    public static readonly Color32 PrimaryBlue = new(18, 101, 230, 255);
    public static readonly Color32 HoverBlue = new(35, 132, 239, 255);
    public static readonly Color32 PressedBlue = new(10, 68, 158, 255);
    public static readonly Color32 PaleBlue = new(235, 246, 255, 255);
    public static readonly Color32 SoftBlue = new(244, 250, 255, 245);
    public static readonly Color32 BorderBlue = new(194, 220, 250, 255);
    public static readonly Color32 MutedText = new(78, 94, 120, 255);
    public static readonly Color32 Green = new(22, 163, 74, 255);
    public static readonly Color32 Red = new(220, 38, 38, 255);
    public static readonly Color32 LightRed = new(255, 237, 237, 255);
    public static readonly Color32 Disabled = new(226, 234, 246, 255);

    static readonly Dictionary<int, Sprite> RoundedSprites = new();
    static readonly Dictionary<string, Sprite> IconSprites = new();

    public static void StylePanel(Image image, Color color, bool shadow = true)
    {
        if (image == null)
            return;

        image.sprite = RoundedSprite(8);
        image.type = Image.Type.Sliced;
        image.color = color;

        if (shadow && image.GetComponent<Shadow>() == null)
        {
            var dropShadow = image.gameObject.AddComponent<Shadow>();
            dropShadow.effectColor = new Color(0.05f, 0.18f, 0.35f, 0.06f);
            dropShadow.effectDistance = new Vector2(0f, -2f);
            dropShadow.useGraphicAlpha = true;
        }
    }

    public static void StyleCircle(Image image, Color color, bool shadow = false)
    {
        if (image == null)
            return;

        image.sprite = RoundedSprite(32);
        image.type = Image.Type.Sliced;
        image.color = color;

        if (shadow && image.GetComponent<Shadow>() == null)
        {
            var dropShadow = image.gameObject.AddComponent<Shadow>();
            dropShadow.effectColor = new Color(0.05f, 0.18f, 0.35f, 0.06f);
            dropShadow.effectDistance = new Vector2(0f, -1f);
            dropShadow.useGraphicAlpha = true;
        }
    }

    public static void StyleButton(Button button, Color normal, Color textColor, bool shadow = true)
    {
        if (button == null)
            return;

        var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = RoundedSprite(6);
            image.type = Image.Type.Sliced;
            image.color = normal;
            button.targetGraphic = image;
        }

        var colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = Blend(normal, Color.white, 0.14f);
        colors.pressedColor = Blend(normal, Color.black, 0.12f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = Disabled;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            text.color = textColor;

        if (shadow && image != null && image.GetComponent<Shadow>() == null)
        {
            var dropShadow = image.gameObject.AddComponent<Shadow>();
            dropShadow.effectColor = new Color(0.05f, 0.18f, 0.35f, 0.06f);
            dropShadow.effectDistance = new Vector2(0f, -1f);
            dropShadow.useGraphicAlpha = true;
        }
    }

    public static Sprite RoundedSprite(int radius)
    {
        radius = Mathf.Clamp(radius, 1, 32);
        if (RoundedSprites.TryGetValue(radius, out var sprite) && sprite != null)
            return sprite;

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "TechWise Rounded UI Sprite",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var colors = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = Mathf.Max(radius - x, 0, x - (size - 1 - radius));
                var dy = Mathf.Max(radius - y, 0, y - (size - 1 - radius));
                var alpha = dx * dx + dy * dy <= radius * radius ? 255 : 0;
                colors[y * size + x] = new Color32(255, 255, 255, (byte)alpha);
            }
        }

        texture.SetPixels32(colors);
        texture.Apply(false, true);

        sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        sprite.name = "TechWise Rounded UI";
        RoundedSprites[radius] = sprite;
        return sprite;
    }

    public static Sprite IconSprite(string key, Color32 color)
    {
        key = string.IsNullOrWhiteSpace(key) ? "Dot" : key;
        var cacheKey = key + ColorUtility.ToHtmlStringRGBA(color);
        if (IconSprites.TryGetValue(cacheKey, out var sprite) && sprite != null)
            return sprite;

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "TechWise Icon " + key,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        texture.SetPixels32(new Color32[size * size]);
        DrawIcon(texture, key, color);
        texture.Apply(false, true);

        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "TechWise Icon " + key;
        IconSprites[cacheKey] = sprite;
        return sprite;
    }

    static Color Blend(Color a, Color b, float amount)
    {
        return Color.Lerp(a, b, amount);
    }

    static void DrawIcon(Texture2D texture, string key, Color color)
    {
        switch (key)
        {
            case "Play":
                FillTriangle(texture, new Vector2Int(21, 14), new Vector2Int(21, 50), new Vector2Int(52, 32), color);
                break;
            case "Desktop":
                Rect(texture, 13, 16, 38, 25, color, 5);
                Line(texture, 32, 41, 32, 49, color, 5);
                Line(texture, 22, 51, 42, 51, color, 5);
                break;
            case "Aim":
                FillTriangle(texture, new Vector2Int(10, 28), new Vector2Int(32, 15), new Vector2Int(54, 28), color);
                Poly(texture, color, 4, new Vector2Int(12, 28), new Vector2Int(32, 17), new Vector2Int(52, 28), new Vector2Int(32, 39), new Vector2Int(12, 28));
                Line(texture, 22, 36, 22, 46, color, 4);
                Line(texture, 42, 36, 42, 46, color, 4);
                Line(texture, 22, 46, 42, 46, color, 4);
                Line(texture, 51, 29, 51, 44, color, 3);
                Circle(texture, 51, 48, 3, color, 3);
                break;
            case "Cup":
                FillRect(texture, 24, 15, 17, 20, color);
                Rect(texture, 23, 15, 18, 21, color, 4);
                Arc(texture, 20, 24, 8, 90, 270, color, 4);
                Arc(texture, 44, 24, 8, 270, 450, color, 4);
                Line(texture, 32, 36, 32, 48, color, 4);
                Line(texture, 22, 50, 42, 50, color, 4);
                break;
            case "Pad":
                Rect(texture, 12, 22, 40, 24, color, 4);
                for (var x = 21; x <= 43; x += 11)
                for (var y = 30; y <= 38; y += 8)
                    Rect(texture, x, y, 3, 3, color, 2);
                break;
            case "Gear":
                FillCircle(texture, 32, 32, 13, color);
                FillCircle(texture, 32, 32, 5, new Color(0, 0, 0, 0));
                for (var i = 0; i < 8; i++)
                {
                    var a = Mathf.Deg2Rad * i * 45f;
                    Line(texture, 32 + Mathf.RoundToInt(Mathf.Cos(a) * 15), 32 + Mathf.RoundToInt(Mathf.Sin(a) * 15), 32 + Mathf.RoundToInt(Mathf.Cos(a) * 22), 32 + Mathf.RoundToInt(Mathf.Sin(a) * 22), color, 5);
                }
                break;
            case "Power":
                Arc(texture, 32, 35, 18, 35, 325, color, 5);
                Line(texture, 32, 13, 32, 32, color, 5);
                break;
            case "User":
                FillCircle(texture, 32, 22, 8, color);
                FillCircle(texture, 32, 46, 15, color);
                break;
            case "VR":
                Rect(texture, 13, 23, 38, 20, color, 4);
                Circle(texture, 25, 33, 5, color, 4);
                Circle(texture, 39, 33, 5, color, 4);
                break;
            case "Cloud":
                FillCircle(texture, 23, 38, 10, color);
                FillCircle(texture, 35, 33, 13, color);
                FillCircle(texture, 46, 39, 9, color);
                FillRect(texture, 18, 38, 32, 7, color);
                break;
            case "Dot":
                FillCircle(texture, 32, 32, 12, color);
                break;
            case "CloudLine":
                Arc(texture, 23, 38, 11, 180, 360, color, 5);
                Arc(texture, 35, 33, 14, 185, 360, color, 5);
                Arc(texture, 46, 39, 9, 200, 360, color, 5);
                Line(texture, 18, 40, 50, 40, color, 5);
                break;
            case "Sync":
                Arc(texture, 32, 32, 19, 35, 310, color, 5);
                Line(texture, 48, 17, 51, 31, color, 5);
                Line(texture, 48, 17, 35, 20, color, 5);
                Line(texture, 16, 47, 13, 33, color, 5);
                Line(texture, 16, 47, 29, 44, color, 5);
                break;
            case "Lock":
                FillRect(texture, 20, 29, 24, 20, color);
                Rect(texture, 20, 29, 24, 20, color, 4);
                Arc(texture, 32, 29, 11, 180, 360, color, 4);
                break;
            case "CPU":
                Rect(texture, 20, 20, 24, 24, color, 4);
                for (var i = 17; i <= 47; i += 10)
                {
                    Line(texture, i, 14, i, 20, color, 3);
                    Line(texture, i, 44, i, 50, color, 3);
                    Line(texture, 14, i, 20, i, color, 3);
                    Line(texture, 44, i, 50, i, color, 3);
                }
                break;
            case "Tool":
                Line(texture, 18, 46, 45, 19, color, 5);
                Line(texture, 39, 17, 49, 27, color, 4);
                Line(texture, 15, 43, 21, 49, color, 4);
                break;
            case "OK":
                Poly(texture, color, 4, new Vector2Int(32, 12), new Vector2Int(48, 19), new Vector2Int(44, 43), new Vector2Int(32, 52), new Vector2Int(20, 43), new Vector2Int(16, 19), new Vector2Int(32, 12));
                Line(texture, 26, 32, 31, 37, color, 4);
                Line(texture, 31, 37, 40, 27, color, 4);
                break;
            case "X":
                Line(texture, 21, 21, 43, 43, color, 5);
                Line(texture, 43, 21, 21, 43, color, 5);
                break;
            default:
                FillCircle(texture, 32, 32, 10, color);
                break;
        }
    }

    static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (var py = y; py <= y + height; py++)
        for (var px = x; px <= x + width; px++)
            Set(texture, px, py, color);
    }

    static void FillCircle(Texture2D texture, int cx, int cy, int radius, Color color)
    {
        for (var y = -radius; y <= radius; y++)
        for (var x = -radius; x <= radius; x++)
        {
            if (x * x + y * y <= radius * radius)
                Set(texture, cx + x, cy + y, color);
        }
    }

    static void Dot(Texture2D texture, int x, int y, Color color, int radius)
    {
        for (var oy = -radius; oy <= radius; oy++)
        for (var ox = -radius; ox <= radius; ox++)
        {
            if (ox * ox + oy * oy <= radius * radius)
                Set(texture, x + ox, y + oy, color);
        }
    }

    static void Set(Texture2D texture, int x, int y, Color color)
    {
        if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
            texture.SetPixel(x, texture.height - 1 - y, color);
    }

    static void Line(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int width)
    {
        var dx = Mathf.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Mathf.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;

        while (true)
        {
            Dot(texture, x0, y0, color, Mathf.Max(1, width / 2));
            if (x0 == x1 && y0 == y1)
                break;
            var e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    static void Rect(Texture2D texture, int x, int y, int width, int height, Color color, int stroke)
    {
        Line(texture, x, y, x + width, y, color, stroke);
        Line(texture, x + width, y, x + width, y + height, color, stroke);
        Line(texture, x + width, y + height, x, y + height, color, stroke);
        Line(texture, x, y + height, x, y, color, stroke);
    }

    static void Circle(Texture2D texture, int cx, int cy, int radius, Color color, int stroke)
    {
        Arc(texture, cx, cy, radius, 0, 360, color, stroke);
    }

    static void Arc(Texture2D texture, int cx, int cy, int radius, int start, int end, Color color, int stroke)
    {
        var previous = Vector2Int.zero;
        var hasPrevious = false;
        for (var d = start; d <= end; d += 4)
        {
            var angle = Mathf.Deg2Rad * d;
            var point = new Vector2Int(cx + Mathf.RoundToInt(Mathf.Cos(angle) * radius), cy + Mathf.RoundToInt(Mathf.Sin(angle) * radius));
            if (hasPrevious)
                Line(texture, previous.x, previous.y, point.x, point.y, color, stroke);
            previous = point;
            hasPrevious = true;
        }
    }

    static void Poly(Texture2D texture, Color color, int stroke, params Vector2Int[] points)
    {
        for (var i = 1; i < points.Length; i++)
            Line(texture, points[i - 1].x, points[i - 1].y, points[i].x, points[i].y, color, stroke);
    }

    static void FillTriangle(Texture2D texture, Vector2Int a, Vector2Int b, Vector2Int c, Color color)
    {
        var minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
        var maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
        var minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
        var maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var p = new Vector2Int(x, y);
            if (SameSide(p, a, b, c) && SameSide(p, b, a, c) && SameSide(p, c, a, b))
                Set(texture, x, y, color);
        }
    }

    static bool SameSide(Vector2Int p1, Vector2Int p2, Vector2Int a, Vector2Int b)
    {
        var cp1 = Cross(b - a, p1 - a);
        var cp2 = Cross(b - a, p2 - a);
        return cp1 * cp2 >= 0;
    }

    static int Cross(Vector2Int a, Vector2Int b) => a.x * b.y - a.y * b.x;
}
