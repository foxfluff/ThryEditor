using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Poiyomi.ModularShaderSystem.Tests
{
    public class GlobalLinkTexturePolicyTests
    {
        [Test]
        public void TexturePolicyPreservesMapsAndSynchronizesShading()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Vulkan).Or.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Direct3D11).Or.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Direct3D12));
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/_PoiyomiShaders/Shaders/10.0/Pro/Poiyomi Pro.shader");
            string folder = "Assets/__GlobalLinkTextures_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
            var textures = new[] { new Texture2D(2, 2), new Texture2D(2, 2) };
            var a=new Material(shader);var b=new Material(shader);
            var passed=new System.Collections.Generic.List<string>();
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);passed.Add(label);};
            var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
            var type=typeof(Thry.ThryEditor.GlobalLinker);
            var cap=type.GetMethod("CapturePropertiesFromSection",flags);
            var apply=type.GetMethod("ApplyLinkToMaterial",flags);
            var recap=type.GetMethod("RecaptureFromMaterial",flags);
            Func<Material,Thry.ThryEditor.ShaderGroup> groupFor=m=>{
             var group=(Thry.ThryEditor.ShaderGroup)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Thry.ThryEditor.ShaderGroup));
             var children=new System.Collections.Generic.List<Thry.ThryEditor.ShaderPart>();
             foreach(var n in new[]{"_LightingAOMaps","_LightDataAOStrengthR"}) {
              var part=(Thry.ThryEditor.ShaderProperty)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Thry.ThryEditor.ShaderProperty));
              typeof(Thry.ThryEditor.ShaderPart).GetProperty("MaterialProperty").GetSetMethod(true).Invoke(part,new object[]{MaterialEditor.GetMaterialProperty(new UnityEngine.Object[]{m},n)});children.Add(part);
             }
             typeof(Thry.ThryEditor.ShaderGroup).GetField("_children",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(group,children);
             return group;
            };
            try {
             for (int i = 0; i < textures.Length; i++)
                 AssetDatabase.CreateAsset(textures[i], folder + "/Texture" + i + ".asset");
             var old=Thry.ThryEditor.Parser.Deserialize<Thry.ThryEditor.GlobalLink>("{\"name\":\"legacy\",\"sectionPropertyName\":\"test\",\"properties\":[],\"subscribedMaterialGuids\":[]}");
             check(old.includeTextures,"Legacy JSON defaults enabled");
             var link=new Thry.ThryEditor.GlobalLink {name="policy test",includeTextures=false};
             var roundtrip=Thry.ThryEditor.Parser.Deserialize<Thry.ThryEditor.GlobalLink>(Thry.ThryEditor.Parser.Serialize(link));
             check(!roundtrip.includeTextures,"Disabled policy survives JSON roundtrip");
             a.SetTexture("_LightingAOMaps",textures[0]);b.SetTexture("_LightingAOMaps",textures[1]);
             a.SetTextureScale("_LightingAOMaps",new Vector2(2,3));b.SetTextureScale("_LightingAOMaps",new Vector2(4,5));
             a.SetTextureOffset("_LightingAOMaps",new Vector2(.1f,.2f));b.SetTextureOffset("_LightingAOMaps",new Vector2(.3f,.4f));
             var tag="_LightingAOMaps"+Thry.ThryEditor.ShaderOptimizer.AnimatedTagSuffix;
             a.SetOverrideTag(tag,"1");b.SetOverrideTag(tag,"2");
             for(int i=0;i<2;i++) {
              var src=i==0?a:b;var dst=i==0?b:a;
              var texture=dst.GetTexture("_LightingAOMaps");var scale=dst.GetTextureScale("_LightingAOMaps");var offset=dst.GetTextureOffset("_LightingAOMaps");var anim=dst.GetTag(tag,false,"");
              src.SetFloat("_LightDataAOStrengthR",i==0?.25f:.65f);
              cap.Invoke(null,new object[]{link,groupFor(src)});
              check(link.properties.All(p=>p.type!="Texture"),"Capture excludes textures direction "+i);
              apply.Invoke(null,new object[]{link,dst,false});
              check(dst.GetFloat("_LightDataAOStrengthR")==src.GetFloat("_LightDataAOStrengthR"),"Shading synchronizes direction "+i);
              check(dst.GetTexture("_LightingAOMaps")==texture&&dst.GetTextureScale("_LightingAOMaps")==scale&&dst.GetTextureOffset("_LightingAOMaps")==offset&&dst.GetTag(tag,false,"")==anim,"Texture transform and tag preserved direction "+i);
             }
             link.includeTextures=true;cap.Invoke(null,new object[]{link,groupFor(a)});link.includeTextures=false;
             var staleTexture=link.properties.First(p=>p.type=="Texture").textureGuid;
             recap.Invoke(null,new object[]{link,b});
             check(link.properties.First(p=>p.type=="Texture").textureGuid==staleTexture,"Undo recapture ignores excluded texture");
             apply.Invoke(null,new object[]{link,b,false});check(b.GetTexture("_LightingAOMaps")==textures[1],"Apply filters stale serialized textures");
             link.includeTextures=true;cap.Invoke(null,new object[]{link,groupFor(a)});apply.Invoke(null,new object[]{link,b,false});
             check(b.GetTexture("_LightingAOMaps")==textures[0]&&b.GetTextureScale("_LightingAOMaps")==new Vector2(2,3)&&b.GetTag(tag,false,"")=="1","Enabled policy preserves full-copy behavior");
             Assert.That(passed.Count, Is.EqualTo(11));
            }finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);AssetDatabase.DeleteAsset(folder);}
        }

        [Test]
        public void EmptyLinkPropertiesSurvivePrettyPrintedJson()
        {
            var data = new Thry.ThryEditor.GlobalLinksData { links = new[] { new Thry.ThryEditor.GlobalLink { name = "empty", includeTextures = false } } };
            var result = Thry.ThryEditor.Parser.Deserialize<Thry.ThryEditor.GlobalLinksData>(Thry.ThryEditor.Parser.Serialize(data, prettyPrint: true));
            Assert.That(result.links.Length, Is.EqualTo(1));
            Assert.That(result.links[0].includeTextures, Is.False);
            Assert.That(result.links[0].properties, Is.Empty);
        }

        [TestCase("none")]
        [TestCase("create")]
        [TestCase("delete")]
        [TestCase("subscriptions")]
        [TestCase("replace")]
        public void PolicyUndoPreservesUnrelatedEdits(string mutation)
        {
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(Thry.ThryEditor.GlobalLinker);
            var dataField = type.GetField("s_data", flags);
            var stateField = type.GetField("s_undoState", flags);
            var appliedField = type.GetField("s_appliedUndoJson", flags);
            var save = type.GetMethod("Save", flags);
            var originalData = Thry.ThryEditor.GlobalLinker.GetAllLinks();
            var originalState = stateField.GetValue(null);
            var originalApplied = appliedField.GetValue(null);
            const string path = "Thry/global_links.json";
            var originalFile = System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null;
            var state = ScriptableObject.CreateInstance(stateField.FieldType);
            state.hideFlags = HideFlags.HideAndDontSave;
            var texture = new Thry.ThryEditor.GlobalLinkPropertyValue { name = "_Map", type = "Texture", textureGuid = "original-map" };
            var shading = new Thry.ThryEditor.GlobalLinkPropertyValue { name = "_Amount", type = "Float", floatValue = 1 };
            var link = new Thry.ThryEditor.GlobalLink { name = "policy", sectionPropertyName = "__test", properties = new[] { texture, shading } };
            var other = new Thry.ThryEditor.GlobalLink { name = "other", sectionPropertyName = "__test" };
            var data = new System.Collections.Generic.List<Thry.ThryEditor.GlobalLink> { link, other };
            try
            {
                dataField.SetValue(null, data);
                stateField.SetValue(null, state);
                save.Invoke(null, null);
                Thry.ThryEditor.GlobalLinker.SetIncludeTextures(link, false, null);
                Assert.That(link.includeTextures, Is.False);
                Assert.That(link.properties.Any(p => p.type == "Texture"), Is.False);
                if (mutation == "create") data.Add(new Thry.ThryEditor.GlobalLink { name = "new" });
                if (mutation == "delete") Thry.ThryEditor.GlobalLinker.DeleteLink(other);
                if (mutation == "subscriptions") link.subscribedMaterialGuids = new[] { "new-subscriber" };
                if (mutation == "replace")
                {
                    Thry.ThryEditor.GlobalLinker.DeleteLink(link);
                    data.Add(new Thry.ThryEditor.GlobalLink { name = link.name, sectionPropertyName = link.sectionPropertyName, includeTextures = false });
                }
                shading.floatValue = 2;
                save.Invoke(null, null);
                var expectedNames = data.Select(l => l.name).ToArray();
                var expectedSubscriptions = data.SelectMany(l => l.subscribedMaterialGuids).ToArray();
                for (int i = 0; i < 4; i++)
                {
                    bool undo = i % 2 == 0;
                    if (undo) Undo.PerformUndo(); else Undo.PerformRedo();
                    var actual = Thry.ThryEditor.GlobalLinker.GetAllLinks();
                    Assert.That(actual.Select(l => l.name), Is.EqualTo(expectedNames));
                    Assert.That(actual.SelectMany(l => l.subscribedMaterialGuids), Is.EqualTo(expectedSubscriptions));
                    var policy = actual.Single(l => l.name == link.name);
                    Assert.That(policy.includeTextures, Is.EqualTo(undo && mutation != "replace"));
                    if (mutation != "replace")
                    {
                        Assert.That(policy.properties.Single(p => p.type == "Float").floatValue, Is.EqualTo(2));
                        Assert.That(policy.properties.Any(p => p.type == "Texture"), Is.EqualTo(undo));
                        if (undo) Assert.That(policy.properties.Single(p => p.type == "Texture").textureGuid, Is.EqualTo(texture.textureGuid));
                    }
                    var persisted = Thry.ThryEditor.Parser.Deserialize<Thry.ThryEditor.GlobalLinksData>(System.IO.File.ReadAllText(path));
                    Assert.That(persisted.links.Select(l => l.name), Is.EqualTo(expectedNames));
                    Assert.That(persisted.links.Single(l => l.name == link.name).includeTextures, Is.EqualTo(policy.includeTextures));
                }
            }
            finally
            {
                Undo.ClearUndo(state);
                UnityEngine.Object.DestroyImmediate(state);
                dataField.SetValue(null, originalData);
                stateField.SetValue(null, originalState);
                appliedField.SetValue(null, originalApplied);
                if (originalFile != null) System.IO.File.WriteAllBytes(path, originalFile);
                else System.IO.File.Delete(path);
            }
        }
    }
}
