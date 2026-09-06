using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Aether.Citadel
{
    /// <summary>
    /// Writes the selected hierarchy out as a single .obj + .mtl (with textures copied
    /// alongside) so the model can be opened in Blender, 3ds Max, Maya or Substance.
    /// </summary>
    public static class ObjExport
    {
        public static void ExportSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("OBJ export", "Select a GameObject first.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel("Export OBJ", "", go.name + ".obj", "obj");
            if (string.IsNullOrEmpty(path)) return;

            Export(go, path);
            EditorUtility.RevealInFinder(path);
        }

        public static void Export(GameObject root, string objPath)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>();
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1 << 22);
            var mtl = new StringBuilder();
            var seenMaterials = new HashSet<string>();
            string dir = Path.GetDirectoryName(objPath);
            string mtlName = Path.GetFileNameWithoutExtension(objPath) + ".mtl";

            sb.AppendLine("# Aether desert citadel - exported from Unity");
            sb.AppendLine("mtllib " + mtlName);

            int vOffset = 1, vtOffset = 1, vnOffset = 1;
            var worldToRoot = root.transform.worldToLocalMatrix;

            for (int f = 0; f < filters.Length; f++)
            {
                var mf = filters[f];
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                var mats = mr != null ? mr.sharedMaterials : new Material[0];

                EditorUtility.DisplayProgressBar("OBJ export", mf.name, (float)f / filters.Length);

                var m = worldToRoot * mf.transform.localToWorldMatrix;
                var verts = mesh.vertices;
                var norms = mesh.normals;
                var uvs = mesh.uv;

                sb.AppendLine("g " + mf.gameObject.name);

                for (int i = 0; i < verts.Length; i++)
                {
                    var p = m.MultiplyPoint3x4(verts[i]);
                    sb.AppendLine(string.Format(ci, "v {0} {1} {2}", -p.x, p.y, p.z));
                }
                for (int i = 0; i < uvs.Length; i++)
                    sb.AppendLine(string.Format(ci, "vt {0} {1}", uvs[i].x, uvs[i].y));
                for (int i = 0; i < norms.Length; i++)
                {
                    var n = m.MultiplyVector(norms[i]).normalized;
                    sb.AppendLine(string.Format(ci, "vn {0} {1} {2}", -n.x, n.y, n.z));
                }

                bool hasUv = uvs.Length == verts.Length;
                bool hasN = norms.Length == verts.Length;

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var mat = s < mats.Length ? mats[s] : null;
                    string matName = mat != null ? Sanitize(mat.name) : "default";
                    if (seenMaterials.Add(matName)) AppendMaterial(mtl, mat, matName, dir);
                    sb.AppendLine("usemtl " + matName);

                    var tris = mesh.GetTriangles(s);
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        int a = tris[i] + vOffset, b = tris[i + 1] + vOffset, c = tris[i + 2] + vOffset;
                        int ta = tris[i] + vtOffset, tb = tris[i + 1] + vtOffset, tc = tris[i + 2] + vtOffset;
                        int na = tris[i] + vnOffset, nb = tris[i + 1] + vnOffset, nc = tris[i + 2] + vnOffset;
                        if (hasUv && hasN)
                            sb.AppendLine(string.Format("f {0}/{1}/{2} {3}/{4}/{5} {6}/{7}/{8}", a, ta, na, b, tb, nb, c, tc, nc));
                        else if (hasN)
                            sb.AppendLine(string.Format("f {0}//{1} {2}//{3} {4}//{5}", a, na, b, nb, c, nc));
                        else
                            sb.AppendLine(string.Format("f {0} {1} {2}", a, b, c));
                    }
                }

                vOffset += verts.Length;
                vtOffset += uvs.Length;
                vnOffset += norms.Length;
            }

            EditorUtility.ClearProgressBar();
            File.WriteAllText(objPath, sb.ToString());
            File.WriteAllText(Path.Combine(dir, mtlName), mtl.ToString());
            Debug.Log("[Citadel] Exported OBJ to " + objPath);
        }

        static void AppendMaterial(StringBuilder mtl, Material mat, string name, string dir)
        {
            var ci = CultureInfo.InvariantCulture;
            mtl.AppendLine("newmtl " + name);
            Color c = mat != null && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            mtl.AppendLine(string.Format(ci, "Kd {0} {1} {2}", c.r, c.g, c.b));
            mtl.AppendLine("Ka 0 0 0");
            mtl.AppendLine("Ks 0.05 0.05 0.05");
            mtl.AppendLine("Ns 12");
            mtl.AppendLine("d 1");
            mtl.AppendLine("illum 2");

            CopyTexture(mtl, mat, "_BaseMap", "map_Kd", dir);
            CopyTexture(mtl, mat, "_BumpMap", "map_Bump", dir);
            mtl.AppendLine();
        }

        static void CopyTexture(StringBuilder mtl, Material mat, string prop, string key, string dir)
        {
            if (mat == null || !mat.HasProperty(prop)) return;
            var tex = mat.GetTexture(prop);
            if (tex == null) return;
            string src = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(src) || !File.Exists(src)) return;
            string file = Path.GetFileName(src);
            string dst = Path.Combine(dir, file);
            try { if (!File.Exists(dst)) File.Copy(src, dst); }
            catch { /* texture stays referenced by name */ }
            mtl.AppendLine(key + " " + file);
        }

        static string Sanitize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_');
            return sb.ToString();
        }
    }
}
