using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Valve.VR;

namespace KerbalVR
{
    public class KerbalVR_Renderer : MonoBehaviour
    {
        public bool hmdIsInitialized = false;
        public bool hmdIsEnabled = false;

        public EVREye hmdEye;

        public CVRCompositor vrCompositor;

        private RenderTexture hmdRenderTexture;
        private Texture_t hmdTexture;
        private VRTextureBounds_t hmdTextureBounds;
        

        // DEBUG
        private int updateCount = 0;


        /// <summary>
        /// Overrides the Start method for a MonoBehaviour plugin.
        /// </summary>
        void Start()
        {
            Utils.LogInfo("KerbalVR_Renderer started. " + this.gameObject.name);

            hmdTextureBounds.uMin = 0.0f;
            hmdTextureBounds.uMax = 1.0f;
            hmdTextureBounds.vMin = 1.0f;
            hmdTextureBounds.vMax = 0.0f;

            hmdRenderTexture = new RenderTexture(1656, 1840, 24, RenderTextureFormat.ARGB32);
            hmdRenderTexture.Create();

            hmdTexture = new Texture_t();
            hmdTexture.eColorSpace = EColorSpace.Auto;
            hmdTexture.eType = ETextureType.DirectX;
            

            Camera cam = gameObject.GetComponent<Camera>();
            cam.targetTexture = hmdRenderTexture;

            DontDestroyOnLoad(gameObject);
        }
        
        void OnRenderImage(RenderTexture src, RenderTexture dest) {
            /*updateCount = (updateCount >= 300) ? 0 : updateCount + 1;
            if (updateCount == 0) {
                Utils.LogInfo(this.gameObject.name +
                    " OnRenderImage init = " + hmdIsInitialized +
                    ", enabled = " + hmdIsEnabled);
            }*/
            
            if (hmdIsInitialized && hmdIsEnabled) {

                //lock (hmdRenderTexture)
                    //lock (src)
                        //lock (vrCompositor) {

                            EVRCompositorError vrCompositorError = EVRCompositorError.None;

                            hmdTexture.handle = src.GetNativeTexturePtr();
                            vrCompositorError = vrCompositor.Submit(hmdEye, ref hmdTexture, ref hmdTextureBounds, EVRSubmitFlags.Submit_Default);
                            if (vrCompositorError != EVRCompositorError.None) {
                                Utils.LogWarning(hmdEye + " failed: (" + (int)vrCompositorError + ") " + vrCompositorError);
                                hmdIsEnabled = false;
                            } /*else {
                                Utils.LogInfo(hmdEye + " submit success");
                            }*/

                        //}

            }
        }

    } // class KerbalVR_Renderer
} // namespace KerbalVR
