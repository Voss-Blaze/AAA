using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 为 Level 1 中 Environments 下所有以 Square 开头的障碍物补齐 BoxCollider2D。
/// 碰撞体贴合精灵的可见像素，与场景中已有的手工碰撞体保持一致的做法。
/// </summary>
public static class SquareObstacleColliderTool
{
    private const string RootName = "Environments";
    private const string NamePrefix = "Square";
    private const byte AlphaThreshold = 16;

    [MenuItem("Tools/Level/补齐 Square 障碍物的 BoxCollider2D")]
    private static void AddMissing()
    {
        Run(refitExisting: false);
    }

    [MenuItem("Tools/Level/重新贴合全部 Square 障碍物碰撞体")]
    private static void RefitAll()
    {
        Run(refitExisting: true);
    }

    private static void Run(bool refitExisting)
    {
        var root = FindRoot();
        if (root == null)
        {
            Debug.LogError("[SquareCollider] 当前场景中找不到名为 \"" + RootName + "\" 的物体。");
            return;
        }

        var pixelCache = new Dictionary<Texture2D, Color[]>();
        var scene = root.gameObject.scene;
        int added = 0, refitted = 0, skipped = 0, failed = 0;

        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
        {
            if (tr == root) continue;
            var go = tr.gameObject;
            if (!go.name.StartsWith(NamePrefix, System.StringComparison.Ordinal)) continue;

            var col = go.GetComponent<BoxCollider2D>();
            var anyCollider = go.GetComponent<Collider2D>();
            if (anyCollider != null && !refitExisting) { skipped++; continue; }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null)
            {
                Debug.LogWarning("[SquareCollider] " + go.name + " 没有 SpriteRenderer 或 sprite，已跳过。");
                failed++;
                continue;
            }

            if (!TryFit(sr.sprite, pixelCache, out var size, out var offset))
            {
                Debug.LogWarning("[SquareCollider] " + go.name + " 像素读取失败，回退为精灵整块边界。");
                size = sr.sprite.bounds.size;
                offset = sr.sprite.bounds.center;
            }

            if (col == null)
            {
                col = Undo.AddComponent<BoxCollider2D>(go);
                added++;
            }
            else
            {
                Undo.RecordObject(col, "Refit Square Collider");
                refitted++;
            }

            col.isTrigger = false;
            col.offset = offset;
            col.size = size;
            EditorUtility.SetDirty(col);
        }

        if (added > 0 || refitted > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log("[SquareCollider] 新增 " + added + " 个，重贴 " + refitted
                  + " 个，跳过已有 " + skipped + " 个，失败 " + failed + " 个。请按 Ctrl+S 保存场景。");
    }

    /// <summary>按精灵不透明像素范围计算局部空间的碰撞体尺寸与偏移。</summary>
    private static bool TryFit(Sprite sprite, Dictionary<Texture2D, Color[]> cache,
        out Vector2 size, out Vector2 offset)
    {
        size = Vector2.zero;
        offset = Vector2.zero;

        var tex = sprite.texture;
        if (tex == null) return false;

        if (!cache.TryGetValue(tex, out var pixels))
        {
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

            // 贴图导入设置通常是 isReadable = false，这里直接从磁盘解码一份可读副本。
            var readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!readable.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(readable);
                return false;
            }
            pixels = readable.GetPixels();
            Object.DestroyImmediate(readable);
            cache[tex] = pixels;
        }

        int width = tex.width, height = tex.height;
        if (pixels.Length < width * height) return false;

        // textureRect 是精灵在贴图上真正占用的像素范围（裁掉了透明留白），用它限定扫描区域即可。
        var scan = sprite.textureRect;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(scan.x));
        int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(scan.xMax) - 1);
        int y0 = Mathf.Max(0, Mathf.FloorToInt(scan.y));
        int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(scan.yMax) - 1);
        if (x1 < x0 || y1 < y0) return false;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        for (int y = y0; y <= y1; y++)
        {
            int row = y * width;
            for (int x = x0; x <= x1; x++)
            {
                if (pixels[row + x].a * 255f <= AlphaThreshold) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        if (maxX < minX || maxY < minY) return false;

        float ppu = sprite.pixelsPerUnit;
        // sprite.bounds 以 sprite.rect（未裁剪的完整矩形）为基准，所以像素换算成
        // 局部坐标时，原点必须减去 rect 的偏移，而不是 textureRect 的偏移。
        Vector2 min = sprite.bounds.min;
        Rect origin = sprite.rect;
        float lx0 = minX - origin.x;
        float lx1 = maxX + 1 - origin.x;
        float ly0 = minY - origin.y;
        float ly1 = maxY + 1 - origin.y;

        size = new Vector2((lx1 - lx0) / ppu, (ly1 - ly0) / ppu);
        offset = new Vector2(min.x + (lx0 + lx1) * 0.5f / ppu, min.y + (ly0 + ly1) * 0.5f / ppu);
        return true;
    }

    private static Transform FindRoot()
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == RootName) return go.transform;
            var found = FindByName(go.transform, RootName);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindByName(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name) return child;
            var deeper = FindByName(child, name);
            if (deeper != null) return deeper;
        }
        return null;
    }
}
