using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using System.IO;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

public static class RebuildBundlesForWebGL
{
    static readonly string SrcDir = "Assets/StreamingAssets/aa/StandaloneWindows64";
    static readonly string DstDir = "Assets/StreamingAssets/aa/WebGL";

    // .uss intentionally excluded — StyleSheets are saved as CSS text for proper reimport
    // (saving as .uss.asset loses m_ComplexSelectors, making selector matching fail at runtime)
    static readonly HashSet<string> ImporterExtensions = new()
    {
        ".uxml", ".tss", ".inputactions"
    };

    static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly BindingFlags SF = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    // Custom element types whose factories create plain VisualElement/Button/etc.
    // USS type selectors (e.g. "PlayButton { }") won't match — transform to class selectors
    // so the factory-injected ".PlayButton" class picks them up automatically.
    // Auto-populated from assembly scan — covers ALL custom VE types, not a hardcoded list.
    static HashSet<string> _customTypeNames;
    static HashSet<string> CustomTypeNames
    {
        get
        {
            if (_customTypeNames != null) return _customTypeNames;
            _customTypeNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type[] types = null;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
                catch { continue; }
                if (types == null) continue;
                foreach (var t in types)
                {
                    if (t == null) continue;
                    try
                    {
                        if (!typeof(UnityEngine.UIElements.VisualElement).IsAssignableFrom(t)) continue;
                        if (t.IsGenericTypeDefinition) continue;
                        if (t.Namespace == null) continue;
                        if (t.Namespace.StartsWith("UnityEngine") || t.Namespace.StartsWith("UnityEditor") ||
                            t.Namespace.StartsWith("Unity.")) continue;
                        _customTypeNames.Add(t.Name);
                    }
                    catch { }
                }
            }
            Debug.Log($"[CSS] Auto-discovered {_customTypeNames.Count} custom VE type names for type→class transform");
            return _customTypeNames;
        }
    }

    [MenuItem("Build/1 - Extract + Rebuild for WebGL")]
    public static void ExtractAndRebuild()
    {
        // ══════════════════════════════════════════════════════════════
        // PASS 1: Load ALL bundles, extract ALL assets, build ref map
        // StyleSheets are deferred — they need the full refMap for
        // asset path resolution during CSS reconstruction.
        // ══════════════════════════════════════════════════════════════

        var srcFull = Path.GetFullPath(SrcDir);
        var bundleFiles = Directory.GetFiles(srcFull, "*.bundle");
        var builds = new List<AssetBundleBuild>();
        int totalExtracted = 0, totalSkipped = 0;

        var refMap = new Dictionary<int, string>();
        var loadedBundles = new List<AssetBundle>();
        var extractedPairs = new List<(Object original, string savePath)>();
        var deferredCSS = new List<(StyleSheet ss, string savePath)>();

        // FIRST: Load ALL bundles so cross-bundle references resolve during LoadAsset
        var bundleByName = new Dictionary<string, AssetBundle>();
        foreach (var bundlePath in bundleFiles)
        {
            var bundleName = Path.GetFileNameWithoutExtension(bundlePath);
            if (bundleName.Contains("monoscripts") || bundleName.Contains("unitybuiltinassets"))
            {
                Debug.Log($"[Extract] Skipping special bundle: {bundleName}");
                continue;
            }
            var ab = AssetBundle.LoadFromFile(bundlePath);
            if (ab == null) { Debug.LogWarning($"[Extract] FAILED: {bundleName}"); continue; }
            loadedBundles.Add(ab);
            bundleByName[bundleName] = ab;
        }
        Debug.Log($"[Extract] Loaded {loadedBundles.Count} bundles (all at once for cross-bundle refs)");

        // THEN: Extract assets — cross-bundle refs now resolve
        foreach (var kvp in bundleByName)
        {
            var bundleName = kvp.Key;
            var ab = kvp.Value;
            var assetNames = ab.GetAllAssetNames();
            var projectPaths = new List<string>();
            var addressNames = new List<string>();

            foreach (var origPath in assetNames)
            {
                var ext = Path.GetExtension(origPath).ToLower();
                bool needsRename = ImporterExtensions.Contains(ext);
                var savePath = needsRename
                    ? Path.ChangeExtension(origPath, ext.TrimStart('.') + ".asset")
                    : origPath;

                var fullDisk = Path.GetFullPath(savePath);

                // Always re-extract .uss, UXML and TSS — refs need refresh each rebuild
                bool isUSS = ext == ".uss";
                bool isUXML = ext == ".uxml" || savePath.EndsWith(".uxml.asset");
                bool isTSS = ext == ".tss" || savePath.EndsWith(".tss.asset");
                if (!isUSS && !isUXML && !isTSS && (File.Exists(fullDisk) || AssetDatabase.LoadAssetAtPath<Object>(savePath) != null))
                {
                    var existing = ab.LoadAsset(origPath);
                    if (existing != null)
                    {
                        refMap[existing.GetInstanceID()] = savePath;
                        MapSubObjects(ab, origPath, savePath, refMap);
                    }
                    projectPaths.Add(savePath);
                    addressNames.Add(origPath);
                    continue;
                }

                var asset = ab.LoadAsset(origPath);
                if (asset == null) { totalSkipped++; continue; }

                var dir = Path.GetDirectoryName(fullDisk);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                bool saved = false;
                bool deferred = false;

                if (asset is ThemeStyleSheet tss)
                {
                    saved = SaveAsAsset(tss, savePath);
                }
                else if (asset is StyleSheet ss)
                {
                    // Defer: CSS reconstruction needs the full refMap for asset paths
                    deferredCSS.Add((ss, savePath));
                    deferred = true;
                }
                else if (asset is Texture2D tex)
                {
                    saved = SaveTexture(tex, Path.GetFullPath(origPath), origPath);
                    if (saved) savePath = origPath;
                }
                else if (asset is TextAsset ta)
                {
                    var textDisk = Path.GetFullPath(origPath);
                    var textDir = Path.GetDirectoryName(textDisk);
                    if (!Directory.Exists(textDir)) Directory.CreateDirectory(textDir);
                    File.WriteAllBytes(textDisk, ta.bytes);
                    saved = true;
                    savePath = origPath;
                }
                else if (asset is GameObject go)
                {
                    saved = SavePrefab(go, origPath);
                    if (saved) savePath = origPath;
                }
                else if (asset is ScriptableObject so)
                {
                    saved = SaveAsAsset(so, savePath);
                }
                else if (asset is Sprite spr)
                {
                    saved = SaveTexture(spr.texture, Path.GetFullPath(origPath), origPath);
                    if (saved) savePath = origPath;
                }
                else
                {
                    saved = SaveAsAsset(asset, savePath);
                }

                if (deferred || saved)
                {
                    totalExtracted++;
                    projectPaths.Add(savePath);
                    addressNames.Add(origPath);
                    refMap[asset.GetInstanceID()] = savePath;
                    MapSubObjects(ab, origPath, savePath, refMap);
                    if (!deferred)
                        extractedPairs.Add((asset, savePath));
                }
                else
                {
                    totalSkipped++;
                }
            }

            if (projectPaths.Count > 0)
            {
                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetNames = projectPaths.ToArray(),
                    addressableNames = addressNames.ToArray()
                });
            }
        }

        Debug.Log($"[Extract] Done. Extracted={totalExtracted}, Skipped={totalSkipped}, Bundles={builds.Count}, RefMap={refMap.Count}, DeferredCSS={deferredCSS.Count}");

        // ══════════════════════════════════════════════════════════════
        // PASS 1.25: Find orphan StyleSheet sub-assets
        // StyleSheets like MergedUSS are sub-assets without a container
        // path — GetAllAssetNames() doesn't return them. We find them
        // via LoadAllAssets<StyleSheet>() and route through CSS
        // reconstruction so they get proper selectors after reimport.
        // ══════════════════════════════════════════════════════════════

        // Extract StyleSheets embedded inside ThemeStyleSheets (MergedUSS etc.)
        // These are referenced via the TSS's "imports" field but have no
        // container path — GetAllAssetNames() and LoadAllAssets<StyleSheet>()
        // both miss them. We reach them through the TSS's serialized imports.
        var deferredIDs = new HashSet<int>();
        foreach (var (dss, _) in deferredCSS)
            deferredIDs.Add(dss.GetInstanceID());

        var orphansByBundle = new Dictionary<string, List<(string savePath, string addressName)>>();
        var usedOrphanPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int orphanTotal = 0;

        foreach (var kvp in bundleByName)
        {
            var bundleName = kvp.Key;
            var ab = kvp.Value;

            foreach (var tss in ab.LoadAllAssets<ThemeStyleSheet>())
            {
                if (tss == null) continue;

                // Collect all StyleSheet references from this TSS via SerializedObject
                // (covers imports, flattenedImportedStyleSheets, and any other field)
                var foundSS = new List<StyleSheet>();
                try
                {
                    var tssSO = new SerializedObject(tss);
                    var iter = tssSO.GetIterator();
                    while (iter.NextVisible(true))
                    {
                        if (iter.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            var obj = iter.objectReferenceValue;
                            if (obj is StyleSheet ss2 && !(obj is ThemeStyleSheet) && ss2 != tss)
                                foundSS.Add(ss2);
                        }
                    }
                }
                catch { }

                if (foundSS.Count == 0) continue;
                Debug.Log($"[Extract] TSS '{tss.name}': {foundSS.Count} StyleSheet refs found via SerializedObject");

                foreach (var impSS in foundSS)
                {
                    if (deferredIDs.Contains(impSS.GetInstanceID())) continue;

                    var rules = RF<Array>(impSS, "m_Rules");
                    if (rules == null || rules.Length == 0) continue;

                    string safeName = impSS.name;
                    if (string.IsNullOrEmpty(safeName)) safeName = $"tss_import_{orphanTotal}";
                    foreach (var c in Path.GetInvalidFileNameChars())
                        safeName = safeName.Replace(c, '_');

                    string savePath = $"Assets/UI/OrphanUSS/{safeName}.uss";
                    if (usedOrphanPaths.Contains(savePath))
                        savePath = $"Assets/UI/OrphanUSS/{bundleName}_{safeName}.uss";
                    usedOrphanPaths.Add(savePath);
                    string addressName = savePath.ToLower().Replace("\\", "/");

                    deferredCSS.Add((impSS, savePath));
                    deferredIDs.Add(impSS.GetInstanceID());
                    refMap[impSS.GetInstanceID()] = savePath;

                    if (!orphansByBundle.ContainsKey(bundleName))
                        orphansByBundle[bundleName] = new List<(string, string)>();
                    orphansByBundle[bundleName].Add((savePath, addressName));

                    Debug.Log($"[Extract] TSS import: {bundleName}/{tss.name} → {impSS.name} ({rules.Length} rules)");
                    orphanTotal++;
                    totalExtracted++;
                }
            }
        }
        Debug.Log($"[Extract] TSS import StyleSheets: {orphanTotal} from {orphansByBundle.Count} bundles");

        // ══════════════════════════════════════════════════════════════
        // PASS 1.5: Reconstruct CSS text for deferred StyleSheets
        // refMap is complete — asset paths are available for url() refs
        // ══════════════════════════════════════════════════════════════

        int ussOK = 0, ussFailed = 0;
        foreach (var (ss, path) in deferredCSS)
        {
            try
            {
                var css = ReconstructCSS(ss, refMap);
                if (string.IsNullOrEmpty(css))
                {
                    Debug.LogWarning($"[CSS] Empty reconstruction for {ss.name}");
                    ussFailed++;
                    continue;
                }

                var fullPath = Path.GetFullPath(path);
                var cssDir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(cssDir)) Directory.CreateDirectory(cssDir);
                File.WriteAllText(fullPath, css, Encoding.UTF8);
                ussOK++;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CSS] Reconstruction failed for {ss.name}: {e.Message}");
                ussFailed++;
            }
        }

        Debug.Log($"[CSS] StyleSheet → USS text: {ussOK} OK, {ussFailed} failed, {_ussTexExtracted} textures auto-extracted to Assets/UI/USSTextures/");

        AssetDatabase.Refresh();

        // Verify reimported StyleSheets: Unity 6 stores selectors per-rule
        int verified = 0;
        foreach (var (ss, path) in deferredCSS)
        {
            var imported = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (imported == null) continue;
            var rules = RF<Array>(imported, "m_Rules");
            if (rules == null || rules.Length == 0) continue;
            var firstRule = rules.GetValue(0);
            var selectors = RF<Array>(firstRule, "m_ComplexSelectors");
            if (selectors != null && selectors.Length > 0)
                verified++;
            else
                Debug.LogWarning($"[CSS] {path}: rules={rules.Length} but first rule has 0 selectors after reimport");
        }
        Debug.Log($"[CSS] Verified: {verified}/{ussOK} StyleSheets have selectors (per-rule)");

        // Add orphan USS to their source bundle's build entry
        // (only include files that were successfully reconstructed)
        foreach (var kvp in orphansByBundle)
        {
            var bundleName = kvp.Key;
            var paths = kvp.Value.Where(p => File.Exists(Path.GetFullPath(p.savePath))).ToList();
            if (paths.Count == 0) continue;

            int idx = builds.FindIndex(b => b.assetBundleName == bundleName);
            if (idx >= 0)
            {
                var entry = builds[idx];
                builds[idx] = new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetNames = entry.assetNames.Concat(paths.Select(p => p.savePath)).ToArray(),
                    addressableNames = entry.addressableNames.Concat(paths.Select(p => p.addressName)).ToArray()
                };
            }
            else
            {
                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetNames = paths.Select(p => p.savePath).ToArray(),
                    addressableNames = paths.Select(p => p.addressName).ToArray()
                });
            }
            Debug.Log($"[Extract] Added {paths.Count} orphan USS to bundle '{bundleName}'");
        }

        // ══════════════════════════════════════════════════════════════
        // PASS 2: Remap cross-asset references using the original objects
        // (StyleSheets don't need remap — they use path-based url() refs)
        // ══════════════════════════════════════════════════════════════

        int remapped = 0, remapFailed = 0, inlineFixed = 0;
        Debug.Log($"[Remap] extractedPairs count: {extractedPairs.Count}");
        foreach (var (original, savePath) in extractedPairs)
        {
            var savedAsset = AssetDatabase.LoadAssetAtPath<Object>(savePath);
            if (savedAsset == null) continue;

            // Standard remap: iterate all ObjectReference properties
            var origSO = new SerializedObject(original);
            var savedSO = new SerializedObject(savedAsset);
            bool changed = false;

            var origProp = origSO.GetIterator();
            while (origProp.NextVisible(true))
            {
                if (origProp.propertyType != SerializedPropertyType.ObjectReference) continue;

                var target = origProp.objectReferenceValue;
                if (target == null) continue;
                if (target == original) continue;

                if (!refMap.TryGetValue(target.GetInstanceID(), out var targetSavePath)) continue;
                if (targetSavePath == savePath) continue;

                var targetSaved = AssetDatabase.LoadAssetAtPath<Object>(targetSavePath);
                if (targetSaved == null)
                {
                    remapFailed++;
                    continue;
                }

                var savedProp = savedSO.FindProperty(origProp.propertyPath);
                if (savedProp == null)
                {
                    remapFailed++;
                    continue;
                }

                if (savedProp.objectReferenceValue != targetSaved)
                {
                    savedProp.objectReferenceValue = targetSaved;
                    changed = true;
                    remapped++;
                }
            }

            if (changed)
                savedSO.ApplyModifiedPropertiesWithoutUndo();

            // ── InlineSheet sub-asset remap ──
            bool isVTA = original is VisualTreeAsset;
            bool hasInlineField = origSO.FindProperty("inlineSheet") != null;
            if (!isVTA && hasInlineField)
            {
                Debug.Log($"[Remap] {original.name}: type={original.GetType().FullName} isVTA=false but HAS inlineSheet field — treating as VTA");
                isVTA = true;
            }
            if (isVTA)
            {
                // NEW APPROACH: work with the SAVED asset directly, not the bundle original
                // The bundle's SerializedObject may be unreliable for cross-bundle refs
                var savedVTAObj = AssetDatabase.LoadAssetAtPath<Object>(savePath);
                if (savedVTAObj != null)
                {
                    // Find the inlineSheet sub-asset
                    StyleSheet savedInlineSheet = null;
                    var allSubs = AssetDatabase.LoadAllAssetsAtPath(savePath);
                    foreach (var sub in allSubs)
                    {
                        if (sub is StyleSheet ss && (sub.name == "inlineSheet" || sub.name.Contains("inline")))
                        { savedInlineSheet = ss; break; }
                    }
                    // Fallback: any StyleSheet sub-asset
                    if (savedInlineSheet == null)
                    {
                        foreach (var sub in allSubs)
                            if (sub is StyleSheet ss2 && sub != savedVTAObj)
                            { savedInlineSheet = ss2; break; }
                    }

                    if (savedInlineSheet != null)
                    {
                        var inlineSO = new SerializedObject(savedInlineSheet);
                        var assetsArr = inlineSO.FindProperty("assets");
                        int arrSize = assetsArr?.isArray == true ? assetsArr.arraySize : -1;
                        // Check ALL entries — even non-null ones may be Instantiate copies without GUID
                        if (assetsArr != null && assetsArr.isArray && assetsArr.arraySize > 0)
                        {
                            bool anyFixed = false;
                            for (int ai = 0; ai < assetsArr.arraySize; ai++)
                            {
                                var el = assetsArr.GetArrayElementAtIndex(ai);
                                var curRef = el.objectReferenceValue;

                                // Check if this ref is a project asset (has a valid path) or an Instantiate copy
                                string curPath = curRef != null ? AssetDatabase.GetAssetPath(curRef) : "";
                                bool needsFix = curRef == null || string.IsNullOrEmpty(curPath);

                                if (!needsFix) continue;

                                // Find the project asset by name
                                string texName = curRef?.name ?? "";
                                Object found = null;

                                if (!string.IsNullOrEmpty(texName))
                                {
                                    // Try Texture2D first, then Sprite
                                    var guids = AssetDatabase.FindAssets($"t:Texture2D {texName}");
                                    if (guids.Length > 0)
                                        found = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
                                    if (found == null)
                                    {
                                        guids = AssetDatabase.FindAssets($"t:Sprite {texName}");
                                        if (guids.Length > 0)
                                            found = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
                                    }
                                    // Also try refMap
                                    if (found == null && curRef != null && refMap.TryGetValue(curRef.GetInstanceID(), out var rmPath))
                                        found = AssetDatabase.LoadAssetAtPath<Object>(rmPath);
                                }

                                if (found != null)
                                {
                                    el.objectReferenceValue = found;
                                    anyFixed = true;
                                    inlineFixed++;
                                    Debug.Log($"[Remap] InlineSheet {original.name}: assets[{ai}] {texName} → {found.name} ({AssetDatabase.GetAssetPath(found)})");
                                }
                                else if (curRef == null)
                                {
                                    Debug.LogWarning($"[Remap] InlineSheet {original.name}: assets[{ai}] NULL and no project asset found");
                                }
                                else
                                {
                                    Debug.LogWarning($"[Remap] InlineSheet {original.name}: assets[{ai}] '{texName}' is Instantiate copy without project path");
                                }
                            }
                            if (anyFixed)
                                inlineSO.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }

                // KEEP OLD CODE BELOW for scalableImages etc.
                var origInlineField = origSO.FindProperty("inlineSheet");
                var origInlineRef = origInlineField?.objectReferenceValue;
                if (origInlineRef == null && origInlineField != null)
                {
                    Debug.Log($"[Remap] {original.name}: inlineSheet field exists but ref is NULL. Checking saved copy...");
                    // The original's inlineSheet might be null (cross-bundle), check the SAVED asset
                    var savedVTA = AssetDatabase.LoadAssetAtPath<Object>(savePath);
                    if (savedVTA != null)
                    {
                        var savedVTASO = new SerializedObject(savedVTA);
                        var savedInlineField = savedVTASO.FindProperty("inlineSheet");
                        if (savedInlineField != null && savedInlineField.objectReferenceValue != null)
                        {
                            var savedInlineSO2 = new SerializedObject(savedInlineField.objectReferenceValue);
                            var savedAssets2 = savedInlineSO2.FindProperty("assets");
                            if (savedAssets2 != null && savedAssets2.isArray)
                            {
                                int nullCount = 0;
                                for (int x = 0; x < savedAssets2.arraySize; x++)
                                    if (savedAssets2.GetArrayElementAtIndex(x).objectReferenceValue == null) nullCount++;
                                Debug.Log($"[Remap] {original.name}: saved inlineSheet has {savedAssets2.arraySize} assets, {nullCount} null");
                            }
                        }
                    }
                }
                if (origInlineField != null && origInlineField.objectReferenceValue != null)
                {
                    var origInline = origInlineField.objectReferenceValue;

                    // Find the saved inlineSheet sub-asset
                    Object savedInline = null;
                    foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(savePath))
                    {
                        if (sub != null && sub.name == "inlineSheet" && sub is StyleSheet)
                        { savedInline = sub; break; }
                    }

                    if (savedInline != null)
                    {
                        var origInlineSO = new SerializedObject(origInline);
                        var savedInlineSO = new SerializedObject(savedInline);

                        // Remap the assets[] array
                        var origAssets = origInlineSO.FindProperty("assets");
                        var savedAssets = savedInlineSO.FindProperty("assets");

                        if (origAssets != null && savedAssets != null &&
                            origAssets.isArray && savedAssets.isArray)
                        {
                            bool inlineChanged = false;
                            int count = Math.Min(origAssets.arraySize, savedAssets.arraySize);
                            for (int ai = 0; ai < count; ai++)
                            {
                                var origEl = origAssets.GetArrayElementAtIndex(ai);
                                var savedEl = savedAssets.GetArrayElementAtIndex(ai);

                                // If saved already has a valid ref, skip
                                if (savedEl.objectReferenceValue != null) continue;

                                var origRef = origEl.objectReferenceValue;

                                // PATH A: origRef is valid (same-bundle texture)
                                if (origRef != null)
                                {
                                    if (refMap.TryGetValue(origRef.GetInstanceID(), out var texPath))
                                    {
                                        var texSaved = AssetDatabase.LoadAssetAtPath<Object>(texPath);
                                        if (texSaved != null)
                                        {
                                            savedEl.objectReferenceValue = texSaved;
                                            inlineChanged = true;
                                            inlineFixed++;
                                            Debug.Log($"[Remap] InlineSheet {original.name}: assets[{ai}] → {texSaved.name} ({texPath})");
                                        }
                                    }
                                    else
                                    {
                                        string texName = origRef.name;
                                        if (!string.IsNullOrEmpty(texName))
                                        {
                                            var guids = AssetDatabase.FindAssets($"t:Texture2D {texName}");
                                            if (guids.Length > 0)
                                            {
                                                var texPath2 = AssetDatabase.GUIDToAssetPath(guids[0]);
                                                var texFound = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath2);
                                                if (texFound != null)
                                                {
                                                    savedEl.objectReferenceValue = texFound;
                                                    inlineChanged = true;
                                                    inlineFixed++;
                                                    Debug.Log($"[Remap] InlineSheet {original.name}: assets[{ai}] → {texFound.name} (by name)");
                                                }
                                            }
                                        }
                                    }
                                }

                                // PATH B: origRef is NULL (cross-bundle dependency not resolved)
                                // Search ALL loaded bundles for the texture that this slot expects
                                if (savedEl.objectReferenceValue == null)
                                {
                                    // Try to find texture by scanning the original VisualTreeAsset's
                                    // property data for hints about what texture was expected
                                    // Strategy: search all bundle textures that match common UI texture names
                                    // and try each one by examining the VTA's serialized references
                                    Object foundTex = null;

                                    // Use the SAVED inlineSheet's internal data to find clues
                                    // The m_Rules array has m_Values with valueIndex pointing to assets[]
                                    // We can look at which rule uses this asset index and try to infer the name
                                    // For now: scan all loaded bundles for textures and build a lookup
                                    foreach (var bkv in loadedBundles)
                                    {
                                        foreach (var subName in bkv.GetAllAssetNames())
                                        {
                                            if (!subName.EndsWith(".png") && !subName.EndsWith(".jpg")) continue;
                                            var tex = bkv.LoadAsset<Texture2D>(subName);
                                            if (tex == null) continue;
                                            // Check if this texture's instanceID is in refMap
                                            if (refMap.TryGetValue(tex.GetInstanceID(), out var diskPath))
                                            {
                                                var diskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diskPath);
                                                if (diskTex != null)
                                                {
                                                    // We need to check if THIS is the texture that belongs at this slot
                                                    // Unfortunately without the original reference we can't be sure
                                                    // Skip for now — we'll use a different approach below
                                                }
                                            }
                                        }
                                    }

                                    // Better approach: load the ORIGINAL VisualTreeAsset from ALL bundles
                                    // and find the one where the inlineSheet.assets[ai] is NOT null
                                    string origName = original.name.Replace(".uxml", "");
                                    foreach (var bkv in loadedBundles)
                                    {
                                        foreach (var assetName in bkv.GetAllAssetNames())
                                        {
                                            if (!assetName.Contains(origName)) continue;
                                            var vta = bkv.LoadAsset<VisualTreeAsset>(assetName);
                                            if (vta == null) continue;
                                            // Get this VTA's inlineSheet
                                            var vtaSO = new SerializedObject(vta);
                                            var isField = vtaSO.FindProperty("inlineSheet");
                                            if (isField == null || isField.objectReferenceValue == null) continue;
                                            var isSO = new SerializedObject(isField.objectReferenceValue);
                                            var isAssets = isSO.FindProperty("assets");
                                            if (isAssets == null || !isAssets.isArray || ai >= isAssets.arraySize) continue;
                                            var bundleRef = isAssets.GetArrayElementAtIndex(ai).objectReferenceValue;
                                            if (bundleRef == null) continue;

                                            // Found it! Now find in project by name
                                            string bName = bundleRef.name;
                                            if (refMap.TryGetValue(bundleRef.GetInstanceID(), out var bPath))
                                            {
                                                foundTex = AssetDatabase.LoadAssetAtPath<Object>(bPath);
                                            }
                                            if (foundTex == null && !string.IsNullOrEmpty(bName))
                                            {
                                                var g = AssetDatabase.FindAssets($"t:Texture2D {bName}");
                                                if (g.Length > 0)
                                                    foundTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g[0]));
                                            }
                                            if (foundTex != null)
                                            {
                                                Debug.Log($"[Remap] InlineSheet {original.name}: assets[{ai}] → {foundTex.name} (cross-bundle resolve from {Path.GetFileName(assetName)})");
                                                break;
                                            }
                                        }
                                        if (foundTex != null) break;
                                    }

                                    if (foundTex != null)
                                    {
                                        savedEl.objectReferenceValue = foundTex;
                                        inlineChanged = true;
                                        inlineFixed++;
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"[Remap] InlineSheet {original.name}: assets[{ai}] NULL — cross-bundle ref unresolved");
                                    }
                                }
                            }

                            if (inlineChanged)
                                savedInlineSO.ApplyModifiedPropertiesWithoutUndo();
                        }

                        // Also remap scalableImages if present
                        var origScalable = origInlineSO.FindProperty("scalableImages");
                        var savedScalable = savedInlineSO.FindProperty("scalableImages");
                        if (origScalable != null && savedScalable != null &&
                            origScalable.isArray && savedScalable.isArray)
                        {
                            bool scalableChanged = false;
                            int sCount = Math.Min(origScalable.arraySize, savedScalable.arraySize);
                            for (int si = 0; si < sCount; si++)
                            {
                                // Each scalableImage has normalImage and highResolutionImage
                                foreach (var fieldName in new[] { "normalImage", "highResolutionImage" })
                                {
                                    var origImg = origScalable.GetArrayElementAtIndex(si).FindPropertyRelative(fieldName);
                                    var savedImg = savedScalable.GetArrayElementAtIndex(si).FindPropertyRelative(fieldName);
                                    if (origImg == null || savedImg == null) continue;
                                    var origTex = origImg.objectReferenceValue;
                                    if (origTex == null) continue;

                                    if (refMap.TryGetValue(origTex.GetInstanceID(), out var imgPath))
                                    {
                                        var imgSaved = AssetDatabase.LoadAssetAtPath<Object>(imgPath);
                                        if (imgSaved != null && savedImg.objectReferenceValue != imgSaved)
                                        {
                                            savedImg.objectReferenceValue = imgSaved;
                                            scalableChanged = true;
                                            inlineFixed++;
                                        }
                                    }
                                }
                            }
                            if (scalableChanged)
                                savedInlineSO.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
            }
        }

        foreach (var ab in loadedBundles)
            ab.Unload(false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Remap] Done. Remapped={remapped}, Failed={remapFailed}, InlineSheet fixes={inlineFixed}");

        // ══════════════════════════════════════════════════════════════
        // PASS 3: Build WebGL bundles
        // ══════════════════════════════════════════════════════════════

        var dstFull = Path.GetFullPath(DstDir);
        if (Directory.Exists(dstFull))
        {
            foreach (var old in Directory.GetFiles(dstFull))
                File.Delete(old);
            Debug.Log("[Build] Cleaned old WebGL bundles");
        }
        if (!Directory.Exists(dstFull)) Directory.CreateDirectory(dstFull);

        Debug.Log($"[Build] Building {builds.Count} bundles for WebGL...");
        var manifest = BuildPipeline.BuildAssetBundles(
            DstDir,
            builds.ToArray(),
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.WebGL);

        if (manifest == null)
        {
            Debug.LogError("[Build] BuildAssetBundles FAILED");
            return;
        }

        var built = manifest.GetAllAssetBundles();
        Debug.Log($"[Build] SUCCESS: {built.Length} bundles");
        foreach (var b in built)
        {
            var size = new FileInfo(Path.Combine(dstFull, b)).Length;
            Debug.Log($"  {b} ({size / 1024}KB)");
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // CSS Reconstruction — converts compiled StyleSheet → USS text
    // so Unity's .uss importer recompiles m_ComplexSelectors.
    //
    // Strategy:
    //   1. Try Unity's internal StyleSheetToUss (via reflection)
    //   2. Fall back to manual reconstruction from internal arrays
    // ══════════════════════════════════════════════════════════════════

    static string ReconstructCSS(StyleSheet ss, Dictionary<int, string> refMap)
    {
        var builtin = TryBuiltinConverter(ss);
        if (!string.IsNullOrEmpty(builtin))
        {
            Debug.Log($"[CSS] {ss.name}: used built-in converter ({builtin.Length} chars)");
            return builtin;
        }

        return ManualReconstructCSS(ss, refMap);
    }

    static string TryBuiltinConverter(StyleSheet ss)
    {
        try
        {
            Type converterType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                converterType = asm.GetType("UnityEngine.UIElements.StyleSheetToUss")
                             ?? asm.GetType("UnityEditor.UIElements.StyleSheets.StyleSheetToUss")
                             ?? asm.GetType("Unity.UI.Builder.StyleSheetToUss");
                if (converterType != null) break;
            }
            if (converterType == null) return null;

            // Look for a method that takes StyleSheet and returns string or writes to StringBuilder
            foreach (var method in converterType.GetMethods(SF))
            {
                var parms = method.GetParameters();
                if (parms.Length == 0 || parms[0].ParameterType != typeof(StyleSheet)) continue;

                if (method.ReturnType == typeof(string))
                {
                    var args = new object[parms.Length];
                    args[0] = ss;
                    var result = method.Invoke(null, args) as string;
                    if (!string.IsNullOrEmpty(result)) return result;
                }
                else if (parms.Length >= 2 && parms[1].ParameterType == typeof(StringBuilder))
                {
                    var sb = new StringBuilder();
                    var args = new object[parms.Length];
                    args[0] = ss;
                    args[1] = sb;
                    method.Invoke(null, args);
                    if (sb.Length > 0) return sb.ToString();
                }
            }
            return null;
        }
        catch (Exception e)
        {
            Debug.Log($"[CSS] Built-in converter failed: {e.Message}");
            return null;
        }
    }

    static string ManualReconstructCSS(StyleSheet ss, Dictionary<int, string> refMap)
    {
        // Unity 6 structure: selectors are PER-RULE (StyleRule.m_ComplexSelectors),
        // NOT per-stylesheet. There is no StyleSheet.m_ComplexSelectors field.
        var rules = RF<Array>(ss, "m_Rules");
        if (rules == null || rules.Length == 0)
        {
            Debug.LogWarning($"[CSS] {ss.name}: no m_Rules");
            return null;
        }

        var strings = RF<string[]>(ss, "strings") ?? Array.Empty<string>();
        var floats = RF<float[]>(ss, "floats") ?? Array.Empty<float>();
        var colors = RF<Color[]>(ss, "colors") ?? Array.Empty<Color>();
        var dimensions = RF<Array>(ss, "dimensions");
        var assets = RF<Object[]>(ss, "assets") ?? Array.Empty<Object>();
        var scalableImages = RF<Array>(ss, "scalableImages");

        var sb = new StringBuilder();

        // @import directives — imports is ImportStruct[] with a styleSheet field
        var imports = RF<Array>(ss, "imports");
        if (imports != null && imports.Length > 0)
        {
            for (int i = 0; i < imports.Length; i++)
            {
                var imp = imports.GetValue(i);
                var impSS = RF<StyleSheet>(imp, "styleSheet");
                if (impSS == null) continue;
                var path = AssetDatabase.GetAssetPath(impSS);
                if (string.IsNullOrEmpty(path))
                    refMap?.TryGetValue(impSS.GetInstanceID(), out path);
                if (!string.IsNullOrEmpty(path))
                    sb.AppendLine($"@import url(\"{path}\");");
            }
            sb.AppendLine();
        }

        int rulesWritten = 0, selectorsFound = 0;
        for (int r = 0; r < rules.Length; r++)
        {
            var rule = rules.GetValue(r);

            // Each StyleRule has its own m_ComplexSelectors[] and m_Properties[]
            var complexSelectors = RF<Array>(rule, "m_ComplexSelectors");
            var properties = RF<Array>(rule, "m_Properties");
            if (properties == null || properties.Length == 0) continue;

            // Build selector text
            string selectorText = null;
            if (complexSelectors != null && complexSelectors.Length > 0)
            {
                selectorsFound += complexSelectors.Length;
                var parts = new List<string>();
                for (int i = 0; i < complexSelectors.Length; i++)
                {
                    var t = ComplexSelectorToCSS(complexSelectors.GetValue(i));
                    if (!string.IsNullOrEmpty(t)) parts.Add(t);
                }
                if (parts.Count > 0) selectorText = string.Join(",\n", parts);
            }
            if (string.IsNullOrEmpty(selectorText)) continue;

            sb.AppendLine(selectorText + " {");
            for (int p = 0; p < properties.Length; p++)
            {
                var line = PropertyToCSS(properties.GetValue(p),
                    strings, floats, colors, dimensions, assets, scalableImages, refMap);
                if (!string.IsNullOrEmpty(line))
                    sb.AppendLine("    " + line);
            }
            sb.AppendLine("}");
            sb.AppendLine();
            rulesWritten++;
        }

        if (rulesWritten == 0)
        {
            Debug.LogWarning($"[CSS] {ss.name}: {rules.Length} rules but 0 had selectors");
            return null;
        }
        Debug.Log($"[CSS] {ss.name}: {rulesWritten} rules, {selectorsFound} selectors");
        return sb.ToString();
    }

    static string ComplexSelectorToCSS(object complexSelector)
    {
        var selectors = RF<Array>(complexSelector, "m_Selectors");
        if (selectors == null || selectors.Length == 0) return "/* empty */";

        var sb = new StringBuilder();
        for (int i = 0; i < selectors.Length; i++)
        {
            var sel = selectors.GetValue(i);
            if (i > 0)
            {
                var rel = RF<int>(sel, "m_PreviousRelationship");
                sb.Append(rel == 1 ? " > " : " ");
            }

            var parts = RF<Array>(sel, "m_Parts");
            if (parts == null) continue;

            for (int j = 0; j < parts.Length; j++)
            {
                var part = parts.GetValue(j);
                var value = RF<string>(part, "m_Value") ?? RF<string>(part, "value") ?? "";
                var type = RF<int>(part, "m_Type");
                if (type == 0) type = RF<int>(part, "type");

                switch (type)
                {
                    case 1: sb.Append('*'); break;
                    case 2:
                        if (CustomTypeNames.Contains(value))
                            sb.Append('.').Append(value);
                        else
                            sb.Append(value);
                        break;
                    case 3: sb.Append('.').Append(value); break;
                    case 4: sb.Append(':').Append(value); break;
                    case 5: sb.Append(':').Append(value); break;
                    case 6: sb.Append('#').Append(value); break;
                    default: sb.Append(value); break;
                }
            }
        }
        return sb.ToString();
    }

    static string PropertyToCSS(object prop, string[] strings, float[] floats,
        Color[] colors, Array dimensions, Object[] assets, Array scalableImages,
        Dictionary<int, string> refMap)
    {
        // Unity 6: property name is from m_Id enum via the "name" property getter,
        // or m_CustomName for custom properties (--var-name)
        var name = GetPropertyName(prop);
        var values = RF<Array>(prop, "m_Values") ?? RF<Array>(prop, "values");
        if (string.IsNullOrEmpty(name) || values == null || values.Length == 0) return null;

        var sb = new StringBuilder();
        sb.Append(name).Append(": ");

        // StyleValueFunction enum (from dump.cs line 1131756):
        // Var=1, Env=2, LinearGradient=3, filters=4-13, MaterialProperty=14
        // Function handle valueIndex indexes floats[], NOT strings[].
        // For var(): [Function(Var=1), Float(argCount), Variable(--name), ...]
        int funcDepth = 0;
        int currentFuncId = 0;
        bool skipArgCount = false;
        for (int i = 0; i < values.Length; i++)
        {
            var handle = values.GetValue(i);
            var vType = RF<int>(handle, "m_ValueType");
            if (vType == 0) vType = RF<int>(handle, "valueType");
            var vIndex = RF<int>(handle, "valueIndex");

            // Spacing: add space between values except after "(" or before ","
            if (i > 0 && vType != 11)
            {
                var prevHandle = values.GetValue(i - 1);
                var prevType = RF<int>(prevHandle, "m_ValueType");
                if (prevType == 0) prevType = RF<int>(prevHandle, "valueType");
                if (prevType != 10)
                    sb.Append(' ');
            }

            switch (vType)
            {
                case 0: break; // Invalid
                case 1: // Keyword
                    sb.Append(KeywordToCSS(vIndex));
                    break;
                case 2: // Float
                    if (skipArgCount) { skipArgCount = false; break; }
                    if (vIndex >= 0 && vIndex < floats.Length)
                        sb.Append(FormatFloat(floats[vIndex]));
                    break;
                case 3: // Dimension
                    if (dimensions != null && vIndex >= 0 && vIndex < dimensions.Length)
                    {
                        var dim = dimensions.GetValue(vIndex);
                        var dv = RF<float>(dim, "value");
                        var du = RF<int>(dim, "unit");
                        sb.Append(FormatFloat(dv)).Append(UnitToCSS(du));
                    }
                    break;
                case 4: // Color
                    if (vIndex >= 0 && vIndex < colors.Length)
                        sb.Append(ColorToCSS(colors[vIndex]));
                    break;
                case 5: // ResourcePath
                    if (vIndex >= 0 && vIndex < strings.Length)
                        sb.Append("resource(\"").Append(strings[vIndex]).Append("\")");
                    break;
                case 6: // AssetReference
                    sb.Append(AssetRefToCSS(assets, vIndex, refMap));
                    break;
                case 7: // Enum
                    if (vIndex >= 0 && vIndex < strings.Length)
                        sb.Append(strings[vIndex]);
                    break;
                case 8: // Variable
                    if (vIndex >= 0 && vIndex < strings.Length)
                    {
                        var varName = strings[vIndex];
                        if (!varName.StartsWith("--")) varName = "--" + varName;
                        if (funcDepth > 0 && currentFuncId == 1)
                            sb.Append(varName);
                        else if (name.StartsWith("--"))
                            sb.Append(varName);
                        else
                            sb.Append("var(").Append(varName).Append(')');
                    }
                    break;
                case 9: // String
                    if (vIndex >= 0 && vIndex < strings.Length)
                        sb.Append('"').Append(strings[vIndex]).Append('"');
                    break;
                case 10: // Function
                    // Format (from MergedUSS debug): Function handle stores
                    // initial/fallback value name in strings[valueIndex].
                    // The NEXT Float handle stores StyleValueFunction enum
                    // in floats[valueIndex] (1=Var, 2=Env, 3=LinearGradient).
                    if (funcDepth > 0) { sb.Append(") "); funcDepth--; }
                    int funcId = 0;
                    if (i + 1 < values.Length)
                    {
                        var peekH = values.GetValue(i + 1);
                        var peekT = RF<int>(peekH, "m_ValueType");
                        if (peekT == 0) peekT = RF<int>(peekH, "valueType");
                        if (peekT == 2) // Float — contains the StyleValueFunction enum
                        {
                            var peekIdx = RF<int>(peekH, "valueIndex");
                            if (peekIdx >= 0 && peekIdx < floats.Length)
                                funcId = (int)floats[peekIdx];
                        }
                    }
                    currentFuncId = funcId;
                    switch (funcId)
                    {
                        case 1: // Var — skip the funcId Float, Variable emits --name
                            sb.Append("var(");
                            funcDepth++;
                            skipArgCount = true;
                            break;
                        case 2: // Env
                            sb.Append("env(");
                            funcDepth++;
                            skipArgCount = true;
                            break;
                        case 3: // LinearGradient
                            sb.Append("linear-gradient(");
                            funcDepth++;
                            skipArgCount = true;
                            break;
                        default:
                            if (vIndex >= 0 && vIndex < strings.Length && !string.IsNullOrEmpty(strings[vIndex]))
                            {
                                sb.Append(strings[vIndex]);
                                sb.Append('(');
                                funcDepth++;
                            }
                            break;
                    }
                    break;
                case 11: // CommaSeparator
                    sb.Append(", ");
                    break;
                case 12: // ScalableImage
                    sb.Append(ScalableImageToCSS(scalableImages, vIndex, refMap));
                    break;
                case 13: // MissingAssetReference
                    if (name == "-unity-font-definition" || name == "-unity-font")
                    {
                        // Try to resolve to actual font file for bundle compilation
                        var fontPath = UnityEditor.AssetDatabase.FindAssets("t:FontAsset DefaultFont");
                        if (fontPath.Length > 0)
                        {
                            var p = UnityEditor.AssetDatabase.GUIDToAssetPath(fontPath[0]);
                            sb.Append($"url(\"/{p}\")");
                        }
                        else
                            sb.Append("resource(\"DefaultFont\")");
                    }
                    else
                        sb.Append("none");
                    break;
            }
        }

        while (funcDepth > 0) { sb.Append(')'); funcDepth--; }
        sb.Append(';');
        return sb.ToString();
    }

    static string GetPropertyName(object prop)
    {
        if (prop == null) return null;
        // Use the "name" property getter — it converts StylePropertyId enum to CSS name
        try
        {
            var getter = prop.GetType().GetProperty("name",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getter != null)
            {
                var val = getter.GetValue(prop) as string;
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        catch { }
        // Fallback: custom property name
        return RF<string>(prop, "m_CustomName") ?? RF<string>(prop, "name");
    }

    static string ColorToCSS(Color c)
    {
        int r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255);
        int g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255);
        int b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255);
        if (c.a >= 0.999f)
            return $"rgb({r}, {g}, {b})";
        return $"rgba({r}, {g}, {b}, {c.a.ToString("F4", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.')})";
    }

    static string FormatFloat(float v)
    {
        if (v == Mathf.Floor(v) && Mathf.Abs(v) < 1e7f)
            return ((int)v).ToString(CultureInfo.InvariantCulture);
        return v.ToString("G", CultureInfo.InvariantCulture);
    }

    static string KeywordToCSS(int keyword)
    {
        return keyword switch
        {
            0 => "inherit",
            1 => "initial",
            2 => "auto",
            3 => "unset",
            4 => "true",
            5 => "false",
            6 => "none",
            _ => "initial",
        };
    }

    static string UnitToCSS(int unit)
    {
        return unit switch
        {
            0 => "",
            1 => "px",
            2 => "%",
            3 => "s",
            4 => "ms",
            5 => "deg",
            6 => "grad",
            7 => "rad",
            8 => "turn",
            _ => "px",
        };
    }

    static int _ussTexExtracted = 0;

    static string ResolveTexturePath(Object asset, Dictionary<int, string> refMap)
    {
        if (asset == null) return null;
        var path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
            refMap?.TryGetValue(asset.GetInstanceID(), out path);
        if (string.IsNullOrEmpty(path))
        {
            var guids = AssetDatabase.FindAssets($"t:Texture2D {asset.name}");
            if (guids.Length > 0) path = AssetDatabase.GUIDToAssetPath(guids[0]);
        }
        // Auto-extract: USS texture not in project → save as PNG
        if (string.IsNullOrEmpty(path))
        {
            Texture2D tex = asset as Texture2D;
            if (tex == null && asset is Sprite spr) tex = spr.texture;
            if (tex != null && tex.width > 1)
            {
                string texDir = "Assets/UI/USSTextures";
                if (!Directory.Exists(Path.GetFullPath(texDir)))
                    Directory.CreateDirectory(Path.GetFullPath(texDir));
                string safeName = asset.name;
                foreach (var c in Path.GetInvalidFileNameChars())
                    safeName = safeName.Replace(c, '_');
                string pngPath = $"{texDir}/{safeName}.png";
                if (SaveTexture(tex, Path.GetFullPath(pngPath), pngPath))
                {
                    AssetDatabase.ImportAsset(pngPath);
                    path = pngPath;
                    _ussTexExtracted++;
                }
            }
        }
        return path;
    }

    static string AssetRefToCSS(Object[] assets, int vIndex, Dictionary<int, string> refMap)
    {
        if (assets == null || vIndex < 0 || vIndex >= assets.Length || assets[vIndex] == null)
            return "none";
        var asset = assets[vIndex];
        var typeName = asset.GetType().Name;
        if (typeName.Contains("FontAsset") || typeName.Contains("Font"))
        {
            var fontGuids = UnityEditor.AssetDatabase.FindAssets("t:FontAsset DefaultFont");
            if (fontGuids.Length > 0)
                return $"url(\"/{UnityEditor.AssetDatabase.GUIDToAssetPath(fontGuids[0])}\")";
            return "resource(\"DefaultFont\")";
        }
        var path = ResolveTexturePath(asset, refMap);
        if (!string.IsNullOrEmpty(path))
            return $"url(\"/{path}\")";
        return $"url(\"{asset.name}\")";
    }

    static string ScalableImageToCSS(Array scalableImages, int vIndex, Dictionary<int, string> refMap)
    {
        if (scalableImages == null || vIndex < 0 || vIndex >= scalableImages.Length)
            return "none";
        var si = scalableImages.GetValue(vIndex);
        var normalImage = RF<Texture2D>(si, "normalImage") ?? RF<Texture2D>(si, "texture");
        if (normalImage != null)
        {
            var path = ResolveTexturePath(normalImage, refMap);
            if (!string.IsNullOrEmpty(path))
                return $"url(\"/{path}\")";
        }
        return "none";
    }

    static T RF<T>(object obj, string name)
    {
        if (obj == null) return default;
        for (var t = obj.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BF);
            if (f != null)
            {
                var val = f.GetValue(obj);
                if (val == null) return default;
                if (val is T typed) return typed;
                if (typeof(T) == typeof(int) && val.GetType().IsEnum)
                    return (T)(object)Convert.ToInt32(val);
                try { return (T)Convert.ChangeType(val, typeof(T)); } catch { }
                return default;
            }
        }
        return default;
    }

    // ══════════════════════════════════════════════════════════════════
    // Existing helper methods
    // ══════════════════════════════════════════════════════════════════

    static void MapSubObjects(AssetBundle ab, string origPath, string mainSavePath, Dictionary<int, string> refMap)
    {
        var subObjects = ab.LoadAssetWithSubAssets(origPath);
        if (subObjects == null) return;
        foreach (var sub in subObjects)
        {
            if (sub == null) continue;
            if (!refMap.ContainsKey(sub.GetInstanceID()))
                refMap[sub.GetInstanceID()] = mainSavePath;
        }
    }

    static bool SaveAsAsset(Object obj, string savePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(Path.GetFullPath(dir)))
                Directory.CreateDirectory(Path.GetFullPath(dir));

            var copy = Object.Instantiate(obj);
            copy.name = obj.name;
            AssetDatabase.CreateAsset(copy, savePath);

            var so = new SerializedObject(obj);
            string[] subAssetFields = { "inlineSheet", "m_InlineSheet" };
            foreach (var fieldName in subAssetFields)
            {
                var prop = so.FindProperty(fieldName);
                if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                var refObj = prop.objectReferenceValue;
                if (refObj == null) continue;

                var subCopy = Object.Instantiate(refObj);
                subCopy.name = fieldName;
                AssetDatabase.AddObjectToAsset(subCopy, savePath);

                var copySO = new SerializedObject(copy);
                var copyProp = copySO.FindProperty(fieldName);
                if (copyProp != null)
                {
                    copyProp.objectReferenceValue = subCopy;
                    copySO.ApplyModifiedPropertiesWithoutUndo();
                }

                // Extract inline textures as standalone PNGs.
                // Sub-objects embedded via Instantiate work in Editor but are NOT
                // included in WebGL bundles (no own GUID/address).
                var origAssets = RF<Object[]>(refObj, "assets");
                if (origAssets != null && origAssets.Length > 0)
                {
                    string vtaName = Path.GetFileNameWithoutExtension(
                        savePath.Replace(".uxml.asset", "").Replace(".uxml", ""));
                    string texDir = "Assets/UI/InlineTextures";
                    if (!Directory.Exists(Path.GetFullPath(texDir)))
                        Directory.CreateDirectory(Path.GetFullPath(texDir));

                    var replacements = new Object[origAssets.Length];
                    int extracted = 0;

                    for (int i = 0; i < origAssets.Length; i++)
                    {
                        var texObj = origAssets[i];
                        if (texObj == null) continue;

                        Texture2D tex2d = texObj as Texture2D;
                        bool isSprite = texObj is Sprite;
                        if (isSprite) tex2d = ((Sprite)texObj).texture;
                        if (tex2d == null || tex2d.width <= 1) continue;

                        string safeName = texObj.name;
                        if (string.IsNullOrEmpty(safeName)) safeName = $"tex{i}";
                        foreach (var c in Path.GetInvalidFileNameChars())
                            safeName = safeName.Replace(c, '_');

                        string pngPath = $"{texDir}/{vtaName}_{i}_{safeName}.png";
                        if (!SaveTexture(tex2d, Path.GetFullPath(pngPath), pngPath)) continue;

                        AssetDatabase.ImportAsset(pngPath);

                        if (isSprite)
                        {
                            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                            if (importer != null)
                            {
                                importer.textureType = TextureImporterType.Sprite;
                                importer.spriteImportMode = SpriteImportMode.Single;
                                importer.SaveAndReimport();
                            }
                            replacements[i] = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                        }
                        else
                        {
                            replacements[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                        }

                        if (replacements[i] != null)
                        {
                            extracted++;
                            Debug.Log($"[SaveAsAsset] Extracted inline {(isSprite ? "sprite" : "texture")}: {texObj.name} → {pngPath}");
                        }
                    }

                    if (extracted > 0)
                    {
                        var inlineSO = new SerializedObject(subCopy);
                        var assetsArr = inlineSO.FindProperty("assets");
                        if (assetsArr != null && assetsArr.isArray)
                        {
                            for (int i = 0; i < Math.Min(assetsArr.arraySize, replacements.Length); i++)
                            {
                                if (replacements[i] == null) continue;
                                assetsArr.GetArrayElementAtIndex(i).objectReferenceValue = replacements[i];
                            }
                            inlineSO.ApplyModifiedPropertiesWithoutUndo();
                        }
                        Debug.Log($"[SaveAsAsset] {obj.name}: {extracted} inline textures extracted to {texDir}");
                    }
                }

                break;
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Extract] SaveAsAsset failed for {savePath}: {e.Message}");
            return false;
        }
    }

    static bool SaveTexture(Texture2D tex, string fullDisk, string assetPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(fullDisk);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (!tex.isReadable)
            {
                var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(tex, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                readable.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                tex = readable;
            }

            byte[] bytes;
            if (assetPath.EndsWith(".jpg") || assetPath.EndsWith(".jpeg"))
                bytes = tex.EncodeToJPG();
            else
                bytes = tex.EncodeToPNG();

            if (bytes == null || bytes.Length == 0) return false;
            File.WriteAllBytes(fullDisk, bytes);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Extract] Texture failed for {assetPath}: {e.Message}");
            return false;
        }
    }

    static bool SavePrefab(GameObject go, string assetPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(Path.GetFullPath(dir)))
                Directory.CreateDirectory(Path.GetFullPath(dir));
            PrefabUtility.SaveAsPrefabAsset(go, assetPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Extract] Prefab failed for {assetPath}: {e.Message}");
            return false;
        }
    }

    [MenuItem("Build/Inspect Bundle Contents")]
    public static void InspectBundles()
    {
        var srcFull = Path.GetFullPath(SrcDir);
        var bundles = Directory.GetFiles(srcFull, "*.bundle");

        foreach (var bundlePath in bundles)
        {
            var bundleName = Path.GetFileNameWithoutExtension(bundlePath);
            var ab = AssetBundle.LoadFromFile(bundlePath);
            if (ab == null) { Debug.Log($"[Inspect] {bundleName}: FAILED"); continue; }

            var names = ab.GetAllAssetNames();
            Debug.Log($"[Inspect] {bundleName}: {names.Length} assets");
            foreach (var n in names.Take(5))
                Debug.Log($"  asset: {n}");
            if (names.Length > 5)
                Debug.Log($"  ... and {names.Length - 5} more");
            ab.Unload(true);
        }
    }

    [MenuItem("Build/Inspect data_assets_all")]
    public static void InspectDataAssets()
    {
        var path = Path.GetFullPath(Path.Combine(SrcDir, "data_assets_all.bundle"));
        if (!File.Exists(path)) { Debug.LogError("[Inspect] data_assets_all.bundle not found"); return; }

        var ab = AssetBundle.LoadFromFile(path);
        if (ab == null) { Debug.LogError("[Inspect] Failed to load data_assets_all"); return; }

        var names = ab.GetAllAssetNames();
        Debug.Log($"[Inspect] data_assets_all: {names.Length} asset names");
        foreach (var n in names)
            Debug.Log($"  name: {n}");

        var allAssets = ab.LoadAllAssets();
        Debug.Log($"[Inspect] LoadAllAssets: {allAssets.Length} loaded");
        foreach (var asset in allAssets)
        {
            if (asset == null) { Debug.Log("  NULL asset"); continue; }
            Debug.Log($"  loaded: '{asset.name}' type={asset.GetType().FullName}");
            try
            {
                var so = new SerializedObject(asset);
                var iter = so.GetIterator();
                var props = new List<string>();
                if (iter.NextVisible(true))
                {
                    do { props.Add($"{iter.propertyPath}:{iter.propertyType}"); }
                    while (iter.NextVisible(false) && props.Count < 15);
                }
                Debug.Log($"    SO: [{string.Join(", ", props)}]");
            }
            catch (System.Exception e) { Debug.Log($"    SO error: {e.Message}"); }
        }

        // Also try loading sub-assets
        foreach (var n in names)
        {
            var subs = ab.LoadAssetWithSubAssets(n);
            if (subs != null && subs.Length > 1)
            {
                Debug.Log($"  sub-assets of '{n}': {subs.Length}");
                foreach (var s in subs)
                    Debug.Log($"    sub: '{s?.name}' type={s?.GetType()?.FullName ?? "NULL"}");
            }
        }

        ab.Unload(true);
    }

    [MenuItem("Build/Find Font Assets in Bundles")]
    public static void FindFontAssets()
    {
        var srcFull = Path.GetFullPath(SrcDir);
        var bundleFiles = Directory.GetFiles(srcFull, "*.bundle");

        foreach (var bundlePath in bundleFiles)
        {
            var bundleName = Path.GetFileNameWithoutExtension(bundlePath);
            if (bundleName.Contains("monoscripts") || bundleName.Contains("unitybuiltinassets")) continue;

            var ab = AssetBundle.LoadFromFile(bundlePath);
            if (ab == null) continue;

            foreach (var asset in ab.LoadAllAssets())
            {
                var tn = asset.GetType().FullName;
                if (tn.Contains("Font") || asset.name.Contains("Lexend") || asset.name.Contains("lexend") || asset.name.Contains("font"))
                {
                    Debug.Log($"[Font] {bundleName}: {asset.name} type={tn}");

                    // Try to save it to project
                    var savePath = $"Assets/Fonts/{asset.name}.asset";
                    var dir = Path.GetDirectoryName(savePath);
                    if (!Directory.Exists(Path.GetFullPath(dir))) Directory.CreateDirectory(Path.GetFullPath(dir));
                    var copy = Object.Instantiate(asset);
                    copy.name = asset.name;
                    try
                    {
                        AssetDatabase.CreateAsset(copy, savePath);
                        Debug.Log($"[Font] SAVED: {savePath}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[Font] Save failed: {e.Message}");
                    }
                }
            }

            ab.Unload(true);
        }
        AssetDatabase.Refresh();
    }

    [MenuItem("Build/0 - Clean Old USS Assets")]
    public static void CleanOldUssAssets()
    {
        var assetsDir = Path.GetFullPath("Assets");
        var oldFiles = Directory.GetFiles(assetsDir, "*.uss.asset", SearchOption.AllDirectories);
        var oldMetas = oldFiles.Select(f => f + ".meta").Where(File.Exists).ToArray();

        if (oldFiles.Length == 0)
        {
            Debug.Log("[Cleanup] No old .uss.asset files found.");
            return;
        }

        foreach (var f in oldFiles) File.Delete(f);
        foreach (var m in oldMetas) File.Delete(m);
        AssetDatabase.Refresh();
        Debug.Log($"[Cleanup] Deleted {oldFiles.Length} .uss.asset files + {oldMetas.Length} .meta files");
    }

    [MenuItem("Build/Inspect StyleSheet Selectors")]
    public static void InspectStyleSheetSelectors()
    {
        var srcFull = Path.GetFullPath(SrcDir);
        var bundles = Directory.GetFiles(srcFull, "*.bundle");
        int withSelectors = 0, withoutSelectors = 0, totalRules = 0, totalSelectors = 0;

        foreach (var bundlePath in bundles)
        {
            var bundleName = Path.GetFileNameWithoutExtension(bundlePath);
            if (bundleName.Contains("monoscripts") || bundleName.Contains("unitybuiltinassets")) continue;

            var ab = AssetBundle.LoadFromFile(bundlePath);
            if (ab == null) continue;

            foreach (var ss in ab.LoadAllAssets<StyleSheet>())
            {
                // Unity 6: selectors are per-rule, not per-stylesheet
                var rules = RF<Array>(ss, "m_Rules");
                int rc = rules?.Length ?? 0;
                int sc = 0;

                if (rules != null)
                {
                    for (int i = 0; i < rules.Length; i++)
                    {
                        var rule = rules.GetValue(i);
                        var cs = RF<Array>(rule, "m_ComplexSelectors");
                        if (cs != null) sc += cs.Length;
                    }
                }

                totalRules += rc;
                totalSelectors += sc;
                if (sc > 0) withSelectors++;
                else
                {
                    withoutSelectors++;
                    if (rc > 0)
                        Debug.LogWarning($"[Inspect] {ss.name}: {rc} rules but 0 selectors (per-rule)");
                }
            }

            ab.Unload(true);
        }

        Debug.Log($"[Inspect] Win64 bundles: {withSelectors} SS with selectors, {withoutSelectors} without, {totalRules} rules, {totalSelectors} selectors");

        // Also dump field names of StyleSheet for debugging
        Debug.Log("[Inspect] StyleSheet fields:");
        foreach (var f in typeof(StyleSheet).GetFields(BF))
            Debug.Log($"  {f.Name}: {f.FieldType.Name} [{(f.IsPublic ? "pub" : "prv")}]");
    }

    [MenuItem("Build/Extract Theme Colors")]
    public static void ExtractThemeColors()
    {
        var srcFull = Path.GetFullPath(SrcDir);
        var bundleFiles = Directory.GetFiles(srcFull, "*.bundle");
        var colors = new Dictionary<string, string>();

        // Two-pass: load ALL bundles first for cross-bundle type resolution
        var allBundles = new List<AssetBundle>();
        foreach (var bundlePath in bundleFiles)
        {
            var bundleName = Path.GetFileNameWithoutExtension(bundlePath);
            if (bundleName.Contains("monoscripts") || bundleName.Contains("unitybuiltinassets")) continue;
            var ab = AssetBundle.LoadFromFile(bundlePath);
            if (ab != null) allBundles.Add(ab);
        }
        Debug.Log($"[ThemeColors] Loaded {allBundles.Count} bundles for search");

        foreach (var ab in allBundles)
        {
            foreach (var asset in ab.LoadAllAssets())
            {
                if (asset == null) continue;

                // Approach 1: C# reflection (works when type resolves)
                var type = asset.GetType();
                object list = null;
                for (var t = type; t != null; t = t.BaseType)
                {
                    var f = t.GetField("m_figmaColorsData", BF);
                    if (f != null) { list = f.GetValue(asset); break; }
                }
                if (list == null)
                {
                    for (var t = type; t != null; t = t.BaseType)
                    {
                        var f = t.GetField("m_figmaThemeData", BF);
                        if (f == null) continue;
                        var themeList = f.GetValue(asset) as System.Collections.IEnumerable;
                        if (themeList == null) continue;
                        foreach (var themeEntry in themeList)
                        {
                            var scriptable = RF<object>(themeEntry, "themeScriptable");
                            if (scriptable == null) continue;
                            var innerList = RF<object>(scriptable, "m_figmaColorsData");
                            if (innerList != null) { list = innerList; break; }
                        }
                        if (list != null) break;
                    }
                }

                if (list != null)
                {
                    var enumerable = list as System.Collections.IEnumerable;
                    if (enumerable != null)
                    {
                        Debug.Log($"[ThemeColors] Found via reflection: {type.Name} '{asset.name}'");
                        foreach (var entry in enumerable)
                        {
                            var varName = RF<string>(entry, "varName");
                            var color = RF<Vector4>(entry, "color");
                            if (string.IsNullOrEmpty(varName)) continue;
                            int r = Mathf.RoundToInt(Mathf.Clamp01(color.x) * 255);
                            int g = Mathf.RoundToInt(Mathf.Clamp01(color.y) * 255);
                            int b2 = Mathf.RoundToInt(Mathf.Clamp01(color.z) * 255);
                            float a = Mathf.Clamp01(color.w);
                            colors[varName] = a >= 0.999f
                                ? $"rgb({r}, {g}, {b2})"
                                : $"rgba({r}, {g}, {b2}, {a.ToString("F4", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.')})";
                        }
                    }
                }

                // Approach 2: SerializedObject (reads binary data even with missing MonoScript)
                if (colors.Count == 0 && asset is ScriptableObject)
                {
                    try
                    {
                        var so = new SerializedObject(asset);
                        var colorsProp = so.FindProperty("m_figmaColorsData");
                        if (colorsProp != null && colorsProp.isArray && colorsProp.arraySize > 0)
                        {
                            Debug.Log($"[ThemeColors] Found via SerializedObject: '{asset.name}' ({colorsProp.arraySize} entries)");
                            for (int ci = 0; ci < colorsProp.arraySize; ci++)
                            {
                                var entry = colorsProp.GetArrayElementAtIndex(ci);
                                var varName = entry.FindPropertyRelative("varName")?.stringValue;
                                var colorProp = entry.FindPropertyRelative("color");
                                if (string.IsNullOrEmpty(varName) || colorProp == null) continue;
                                float cx = colorProp.FindPropertyRelative("x")?.floatValue ?? 0;
                                float cy = colorProp.FindPropertyRelative("y")?.floatValue ?? 0;
                                float cz = colorProp.FindPropertyRelative("z")?.floatValue ?? 0;
                                float cw = colorProp.FindPropertyRelative("w")?.floatValue ?? 1;
                                int r = Mathf.RoundToInt(Mathf.Clamp01(cx) * 255);
                                int g = Mathf.RoundToInt(Mathf.Clamp01(cy) * 255);
                                int b2 = Mathf.RoundToInt(Mathf.Clamp01(cz) * 255);
                                colors[varName] = cw >= 0.999f
                                    ? $"rgb({r}, {g}, {b2})"
                                    : $"rgba({r}, {g}, {b2}, {cw.ToString("F4", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.')})";
                            }
                        }

                        // Also check m_figmaThemeData (manager with multiple themes)
                        var themeDataProp = so.FindProperty("m_figmaThemeData");
                        if (colors.Count == 0 && themeDataProp != null && themeDataProp.isArray)
                        {
                            Debug.Log($"[ThemeColors] Found m_figmaThemeData via SO: '{asset.name}' ({themeDataProp.arraySize} themes)");
                            for (int ti = 0; ti < themeDataProp.arraySize; ti++)
                            {
                                var themeEntry = themeDataProp.GetArrayElementAtIndex(ti);
                                var scriptableProp = themeEntry.FindPropertyRelative("themeScriptable");
                                if (scriptableProp == null || scriptableProp.objectReferenceValue == null) continue;
                                var innerSO = new SerializedObject(scriptableProp.objectReferenceValue);
                                var innerColors = innerSO.FindProperty("m_figmaColorsData");
                                if (innerColors == null || !innerColors.isArray) continue;
                                Debug.Log($"[ThemeColors]   Theme[{ti}]: {innerColors.arraySize} colors");
                                for (int ci = 0; ci < innerColors.arraySize; ci++)
                                {
                                    var entry = innerColors.GetArrayElementAtIndex(ci);
                                    var varName = entry.FindPropertyRelative("varName")?.stringValue;
                                    var colorProp = entry.FindPropertyRelative("color");
                                    if (string.IsNullOrEmpty(varName) || colorProp == null) continue;
                                    float cx = colorProp.FindPropertyRelative("x")?.floatValue ?? 0;
                                    float cy = colorProp.FindPropertyRelative("y")?.floatValue ?? 0;
                                    float cz = colorProp.FindPropertyRelative("z")?.floatValue ?? 0;
                                    float cw = colorProp.FindPropertyRelative("w")?.floatValue ?? 1;
                                    int r = Mathf.RoundToInt(Mathf.Clamp01(cx) * 255);
                                    int g = Mathf.RoundToInt(Mathf.Clamp01(cy) * 255);
                                    int b2 = Mathf.RoundToInt(Mathf.Clamp01(cz) * 255);
                                    if (!colors.ContainsKey(varName))
                                        colors[varName] = cw >= 0.999f
                                            ? $"rgb({r}, {g}, {b2})"
                                            : $"rgba({r}, {g}, {b2}, {cw.ToString("F4", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.')})";
                                }
                                if (colors.Count > 0) break;
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log($"[ThemeColors] SO scan '{asset.name}': {e.Message}");
                    }
                }
            }

            if (colors.Count > 0) break;
        }

        if (colors.Count == 0)
        {
            Debug.LogWarning("[ThemeColors] No theme colors found. Dumping SO properties via SerializedObject:");
            foreach (var ab2 in allBundles)
            {
                foreach (var asset in ab2.LoadAllAssets<ScriptableObject>())
                {
                    if (asset == null) continue;
                    try
                    {
                        var so = new SerializedObject(asset);
                        var prop = so.GetIterator();
                        var props = new List<string>();
                        while (prop.NextVisible(false) && props.Count < 8)
                            props.Add($"{prop.name}({prop.propertyType})");
                        if (props.Count > 0)
                            Debug.Log($"  '{asset.name}' SO props=[{string.Join(", ", props)}]");
                    }
                    catch { }
                }
            }
            foreach (var b in allBundles) b.Unload(true);
            return;
        }

        var resDir = Path.GetFullPath("Assets/Resources");
        if (!Directory.Exists(resDir)) Directory.CreateDirectory(resDir);

        var sb = new StringBuilder();
        sb.AppendLine("{");
        var keys = colors.Keys.ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            sb.Append($"  \"{keys[i]}\": \"{colors[keys[i]]}\"");
            if (i < keys.Count - 1) sb.Append(',');
            sb.AppendLine();
        }
        sb.AppendLine("}");

        var jsonPath = "Assets/Resources/ThemeColors.json";
        File.WriteAllText(Path.GetFullPath(jsonPath), sb.ToString(), Encoding.UTF8);
        foreach (var b in allBundles) b.Unload(true);
        AssetDatabase.Refresh();
        Debug.Log($"[ThemeColors] Wrote {colors.Count} colors to {jsonPath}");
    }
}
