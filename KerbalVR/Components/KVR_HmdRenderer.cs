using System;
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

        private Texture_t hmdEyeTexture;
        private VRTextureBounds_t hmdTextureBounds;
        private RenderTexture hmdEyeRenderTexture;

        public void Init(EVREye eye, RenderTexture rt) {
            Eye = eye;
            hmdEyeRenderTexture = rt;

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

            hmdEyeTexture = new Texture_t() {
                eColorSpace = EColorSpace.Auto,
                eType = textureType,
            };

            // set rendering bounds on texture to render
            hmdTextureBounds.uMin = 0.0f;
            hmdTextureBounds.uMax = 1.0f;
            hmdTextureBounds.vMin = 1.0f; // flip the vertical coordinate for some reason
            hmdTextureBounds.vMax = 0.0f;

            Utils.Log(gameObject.name + " init");
        }
        
        public void OnRenderImage(RenderTexture src, RenderTexture dest) {

            Utils.Log("OnRenderImage " + gameObject.name);

            if (isPoseUpdated) {
                lock (hmdEyeRenderTexture)
                    lock (src)  {
                        hmdEyeTexture.handle = src.GetNativeTexturePtr();
                        EVRCompositorError error = EVRCompositorError.None;
                        error = OpenVR.Compositor.Submit(Eye, ref hmdEyeTexture, ref hmdTextureBounds, EVRSubmitFlags.Submit_Default);
                    }

                if (Eye == EVREye.Eye_Left) {
                    isLeftEyeRendered = true;
                } else {
                    isRightEyeRendered = true;
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
