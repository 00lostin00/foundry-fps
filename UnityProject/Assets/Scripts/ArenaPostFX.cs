using UnityEngine;

namespace Foundry
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaPostFX : MonoBehaviour
    {
        static Shader shader;
        Material material;

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                if (shader == null) shader = Resources.Load<Shader>("Foundry/Shaders/ArenaPostFX");
                if (shader == null) { Graphics.Blit(source, destination); return; }
                material = new Material(shader);
            }
            material.SetFloat("_Vignette", .35f);
            material.SetFloat("_Saturation", 1.12f);
            material.SetFloat("_Contrast", 1.01f);
            material.SetFloat("_Exposure", .90f);
            material.SetVector("_Tint", new Vector4(1.02f, 1f, .985f, 0));
            Graphics.Blit(source, destination, material);
        }
    }
}
