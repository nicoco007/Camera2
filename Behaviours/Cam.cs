﻿//#define FPSCOUNT

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IPA.Utilities;
using Camera2.Interfaces;
using Camera2.Middlewares;
using Camera2.Configuration;
using Camera2.Utils;
using System.Reflection;

namespace Camera2.Behaviours {

	class Cam2 : MonoBehaviour {
		internal new string name { get; private set; }
		internal string configPath { get { return ConfigUtil.GetCameraPath(name); } }

		internal Camera UCamera { get; private set; } = null;
		internal CameraSettings settings { get; private set; } = null;
		internal RenderTexture renderTexture { get; private set; } = null;

		internal LessRawImage screenImage { get; private set; } = null;
		internal PositionableCam worldCam { get; private set; } = null;

		internal List<IMHandler> middlewares { get; private set; } = new List<IMHandler>();
		
		public void Awake() {
			DontDestroyOnLoad(this);
		}

		public void SetParent(Transform parent) {
			if(transform.parent == parent)
				return;

			transform.parent = parent;
			//transform.SetParent(parent, true);

			if(parent == null) {
				DontDestroyOnLoad(this);

				// Previous parent might've messed up the rot/pos, so lets fix it.
				settings.ApplyPositionAndRotation();
			}
		}

		internal void UpdateRenderTextureAndView() {
			var w = (int)Math.Round(settings.viewRect.width * settings.renderScale);
			var h = (int)Math.Round(settings.viewRect.height * settings.renderScale);

			var sizeChanged = renderTexture?.width != w || renderTexture?.height != h || renderTexture?.antiAliasing != settings.antiAliasing;

			if(sizeChanged) {
				renderTexture?.Release();
				renderTexture = new RenderTexture(w, h, 24) { //, RenderTextureFormat.ARGB32
					autoGenerateMips = false,
					antiAliasing = settings.antiAliasing,
					anisoLevel = 1,
					useDynamicScale = false
				};

				UCamera.aspect = (float)w / (float)h;
				UCamera.targetTexture = renderTexture;
				worldCam?.SetSource(this);
			}

			if(sizeChanged || screenImage.position.x != settings.viewRect.x || screenImage.position.y != settings.viewRect.y)
				screenImage?.SetSource(this);
		}

		internal void ShowWorldCamIfNecessary() {
			if(worldCam == null)
				return;

			bool doShowCam = 
				gameObject.activeInHierarchy && 
				settings.type == Configuration.CameraType.Positionable &&
				settings.worldCamVisibility != WorldCamVisibility.Hidden &&
				(settings.worldCamVisibility != WorldCamVisibility.HiddenWhilePlaying || !SceneUtil.isSongPlaying);

			worldCam.gameObject.SetActive(doShowCam);
		}

		public void Init(string name, LessRawImage presentor, bool loadConfig = false) {
			this.name = name;
			screenImage = presentor;

			var camClone = Instantiate(SceneUtil.GetMainCameraButReally());
			camClone.name = "Cam";


			UCamera = camClone.GetComponent<Camera>();
			UCamera.enabled = false;
			UCamera.clearFlags = CameraClearFlags.SolidColor;
			UCamera.stereoTargetEye = StereoTargetEyeMask.None;
			//UCamera.depthTextureMode = DepthTextureMode.None;
			//UCamera.renderingPath = RenderingPath.DeferredLighting;


			foreach(var child in camClone.transform.Cast<Transform>())
				Destroy(child.gameObject);
			
			var trash = new string[] { "AudioListener", "LIV", "MainCamera", "MeshCollider" };
			foreach(var component in camClone.GetComponents<Behaviour>())
				if(trash.Contains(component.GetType().Name)) Destroy(component);

			camClone.transform.parent = transform;
			camClone.transform.localRotation = Quaternion.identity;
			camClone.transform.localPosition = Vector3.zero;


			//Cloning post process stuff to make it controlable on a per camera basis
			//BloomShite.InstantiateBloomForCamera(UCamera).tag = null;
			//typeof(VisualEffectsController)
			//.GetField("_depthTextureEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
			//.SetValue(camClone.GetComponent<VisualEffectsController>(), new BoolSO() { value = UCamera.depthTextureMode != DepthTextureMode.None });



			worldCam = new GameObject("WorldCam").AddComponent<PositionableCam>();
			worldCam.transform.parent = camClone.transform;

			settings = new CameraSettings(this);
			settings.Load(loadConfig);


			AddTransformer<FPSLimiter>();
			AddTransformer<Smoothfollow>();
			AddTransformer<ModmapExtensions>();
			AddTransformer<Follow360>();
			AddTransformer<MovementScriptProcessor>();

#if DEV
			AddTransformer<PostProcessor>();
#endif
		}

		private void AddTransformer<T>() where T: CamMiddleware, IMHandler {
			middlewares.Add(gameObject.AddComponent<T>().Init(this));
		}

		internal float timeSinceLastRender { get; private set; } = 0f;

		private bool hadUpdate = false;
		private void Update() {
			timeSinceLastRender += Time.deltaTime;
			hadUpdate = true;
		}

#if FPSCOUNT
		int renderedFrames = 0;
		System.Diagnostics.Stopwatch sw = null;
#endif
		private void OnGUI() {
			if(UCamera != null && renderTexture != null && hadUpdate) {
#if FPSCOUNT
				if(sw == null) {
					sw = new System.Diagnostics.Stopwatch();
					sw.Start();
				}
#endif

				foreach(var t in middlewares) {
					if(!t.Pre())
						return;
				}

				hadUpdate = false;
				UCamera.Render();

				foreach(var t in middlewares)
					t.Post();

				timeSinceLastRender = 0f;
#if FPSCOUNT
				renderedFrames++;
				if(sw.ElapsedMilliseconds > 500) {
					Console.WriteLine("Rendered FPS for {1}: {0}", renderedFrames * 2, name);
					renderedFrames = 0;
					sw.Restart();
				}
#endif
			}
		}
		
		private void OnEnable() {
			if(screenImage != null) screenImage.gameObject?.SetActive(true);
			ShowWorldCamIfNecessary();
		}
		
		private void OnDisable() {
			if(screenImage != null) screenImage.gameObject?.SetActive(false);
			ShowWorldCamIfNecessary();
		}
		
		private void OnDestroy() {
			gameObject.SetActive(false);

			foreach(var component in UCamera.gameObject.GetComponents<Behaviour>())
				if(component.GetType() != typeof(Camera))
					Destroy(component);

			if(UCamera != null) Destroy(UCamera);
			if(screenImage != null) Destroy(screenImage);
			Destroy(gameObject);
		}
	}
}
