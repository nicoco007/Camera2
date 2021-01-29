﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Camera2.Interfaces;
using Camera2.Middlewares;
using Camera2.Configuration;
using Camera2.Utils;

namespace Camera2.Behaviours {

	class Cam2 : MonoBehaviour {
		internal new string name { get; private set; }
		internal string configPath { get { return ConfigUtil.GetCameraPath(name); } }

		internal Camera UCamera { get; private set; }
		internal CameraSettings settings { get; private set; }
		internal RenderTexture renderTexture { get; private set; }

		internal LessRawImage screenImage { get; private set; }
		internal PositionableCam worldCam { get; private set; }

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

		internal void UpdateRenderTexture() {
			var w = (int)Math.Round(settings.viewRect.width * settings.renderScale);
			var h = (int)Math.Round(settings.viewRect.height * settings.renderScale);

			if(renderTexture?.width == w && renderTexture?.height == h && renderTexture?.antiAliasing == settings.antiAliasing)
				return;

			renderTexture?.Release();
			renderTexture = new RenderTexture(w, h, 24) { //, RenderTextureFormat.ARGB32
				autoGenerateMips = false,
				antiAliasing = settings.antiAliasing,
				anisoLevel = 1,
				useDynamicScale = false
			};

			UCamera.targetTexture = renderTexture;

			screenImage?.SetSource(this);
			worldCam?.SetSource(this);
		}

		internal void ShowWorldCamIfNecessary() {
			bool doShowCam = true;

			if(settings.worldCamVisibility == WorldCamVisibility.OnlyInPause && SceneUtil.isSongPlaying)
				doShowCam = false;

			if(settings.type != Configuration.CameraType.Positionable || settings.worldCamVisibility == WorldCamVisibility.Never)
				doShowCam = false;

			worldCam?.gameObject.SetActive(doShowCam);
		}

		public void Init(string name, LessRawImage presentor, bool loadConfig = false) {
			this.name = name;
			screenImage = presentor;

			var camClone = Instantiate(Camera.main.gameObject);
			camClone.name = "Cam";


			UCamera = camClone.GetComponent<Camera>();
			UCamera.enabled = false;
			UCamera.clearFlags = CameraClearFlags.SolidColor;
			//UCamera.backgroundColor = new Color(0, 0, 0, 0);
			UCamera.stereoTargetEye = StereoTargetEyeMask.None;


			foreach(var child in camClone.transform.Cast<Transform>())
				Destroy(child.gameObject);

			//TODO: Not sure if VisualEffectsController is really unnecessary, doesnt seem to do anything currently?
			var trash = new string[] { "AudioListener", "LIV", "MainCamera", "MeshCollider", "VisualEffectsController" };
			foreach(var component in camClone.GetComponents<Behaviour>())
				if(trash.Contains(component.GetType().Name)) Destroy(component);


			camClone.transform.parent = transform;
			camClone.transform.localRotation = Quaternion.identity;
			camClone.transform.localPosition = Vector3.zero;


			//TODO: maybe clone the effectcontroller+>_mainEffectContainer+>_mainEffect so we can customize bloom on a per-camera basis
			
			worldCam = new GameObject("WorldCam").AddComponent<PositionableCam>();
			worldCam.transform.parent = camClone.transform;

			settings = new CameraSettings(this);
			settings.Load(loadConfig);


			AddTransformer<FPSLimiter>();
			AddTransformer<Smoothfollow>();
			AddTransformer<ModmapExtensions>();
			AddTransformer<Follow360>();
			AddTransformer<MovementScriptProcessor>();
		}

		private void AddTransformer<T>() where T: CamMiddleware, IMHandler {
			middlewares.Add(gameObject.AddComponent<T>().Init(this));
		}

		internal float timeSinceLastRender { get; private set; } = 0f;

		private void Update() {
			if(UCamera != null && renderTexture != null) {
				timeSinceLastRender += Time.deltaTime;

				foreach(var t in middlewares) {
					if(!t.Pre())
						return;
				}

				UCamera.Render();

				foreach(var t in middlewares)
					t.Post();

				timeSinceLastRender = 0f;
			}
		}
		
		private void OnEnable() {
			screenImage?.gameObject.SetActive(true);
			worldCam?.gameObject.SetActive(true);
		}
		
		private void OnDisable() {
			screenImage?.gameObject.SetActive(false);
			ShowWorldCamIfNecessary();
		}
		
		private void OnDestroy() {
			Destroy(UCamera);
			Destroy(screenImage);
		}
	}
}