using Camera2.Behaviours;
using Camera2.Configuration;
using UnityEngine;

namespace Camera2.Interfaces {
	abstract class CamMiddleware : MonoBehaviour {
		protected Cam2 cam;
		protected CameraSettings settings { get { return cam.settings; } }

		public CamMiddleware Init(Cam2 cam) {
			this.cam = cam;
			return this;
		}
		// Prevents the cam from rendering this frame if returned false
		public virtual bool Pre() { return true; }
		public virtual void Post() { }
		public virtual void CamConfigReloaded() { }
	}
}
