using UnityEngine;
using Valve.VR;

namespace KerbalVR.Components
{
    public class KVR_HmdRenderer : MonoBehaviour
    {
        public static bool isPoseUpdated = false;

        private static bool isLeftEyeRendered = false;
        private static bool isRightEyeRendered = false;

        public EVREye Eye { get; private set; }
        public Texture_t HmdEyeTexture { get; private set; }

        public void Init(EVREye eye) {
            Eye = eye;
            HmdEyeTexture = new Texture_t();
            HmdEyeTexture.eColorSpace = EColorSpace.Auto;

            ETextureType textureType = ETextureType.DirectX;
            switch (SystemInfo.graphicsDeviceType) {
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore:
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2:
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3:
                    textureType = ETextureType.OpenGL;
                    throw new InvalidOperationException(SystemInfo.graphicsDeviceType.ToString() + " does not support VR. You must use -force-d3d12");
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D9:
                    throw new InvalidOperationException(SystemInfo.graphicsDeviceType.ToString() + " does not support VR. You must use -force-d3d12");
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D11:
                    textureType = ETextureType.DirectX;
                    break;
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D12:
                    textureType = ETextureType.DirectX;
                    break;
                default:
                    throw new InvalidOperationException(SystemInfo.graphicsDeviceType.ToString() + " not supported");
            }

            HmdEyeTexture.eType = textureType;
        }
        
        public void OnRenderImage(RenderTexture src, RenderTexture dest) {
            if (isPoseUpdated) {
                if (eye == EVREye.Eye_Left) {
                    HmdEyeTexture.handle = src.GetNativeTexturePtr();

                    EVRCompositorError error = EVRCompositorError.None;
                    error = OpenVR.Compositor.Submit(eye, ref hmdLeftEyeTexture, ref hmdTextureBounds, EVRSubmitFlags.Submit_Default);
                }
            }

            if (isLeftEyeRendered && isRightEyeRendered) {
                isPoseUpdated = false;
                isLeftEyeRendered = false;
                isRightEyeRendered = false;
            }
        }
    }
}
