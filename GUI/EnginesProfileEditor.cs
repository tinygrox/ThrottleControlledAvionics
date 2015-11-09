﻿//   EnginesPrfileEditor.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThrottleControlledAvionics
{
	[KSPAddon(KSPAddon.Startup.EditorAny, false)]
	public class EnginesProfileEditor : AddonWindowBase<EnginesProfileEditor>
	{
		const string LockName = "EnginesProfileEditor";
		const string DefaultConstractName = "Untitled Space Craft";

		NamedConfig CFG;
		readonly List<EngineWrapper> Engines = new List<EngineWrapper>();

		public static bool GUIVisible 
		{ 
			get { return instance != null && instance.CFG != null && instance.CFG.GUIVisible; } 
			set { if(instance != null && instance.CFG != null) instance.CFG.GUIVisible = value; }
		}

		public override void Awake()
		{
			base.Awake();
			width = 600;
			height = 400;
			GameEvents.onEditorShipModified.Add(OnShipModified);
			GameEvents.onEditorLoad.Add(OnShipLoad);
			GameEvents.onEditorRestart.Add(Reset);
		}

		public override void OnDestroy ()
		{
			GameEvents.onEditorShipModified.Remove(OnShipModified);
			GameEvents.onEditorLoad.Remove(OnShipLoad);
			GameEvents.onEditorRestart.Remove(Reset);
			TCAMacroEditor.Exit();
			base.OnDestroy();
		}

		void Reset() { reset = true; }

		void OnShipLoad(ShipConstruct ship, CraftBrowser.LoadType load_type)
		{ init_engines = load_type == CraftBrowser.LoadType.Normal; }

		void GetCFG(ShipConstruct ship)
		{
			var TCA_Modules = ModuleTCA.AllTCA(ship);
			if(TCA_Modules.Count == 0) { Reset(); return; }
			CFG = null;
			foreach(var tca in TCA_Modules)
			{
				if(tca.CFG == null) continue;
				CFG = NamedConfig.FromVesselConfig(ship.shipName, tca.CFG);
				break;
			}
			if(CFG == null)
			{
				CFG = new NamedConfig(ship.shipName);
				CFG.EnginesProfiles.AddProfile(Engines);
			}
			else CFG.ActiveProfile.Apply(Engines);
			CFG.ActiveProfile.Update(Engines);
			UpdateCFG(TCA_Modules);
		}

		void UpdateCFG(IList<ModuleTCA> TCA_Modules)
		{
			if(CFG == null || TCA_Modules.Count == 0) return;
			TCA_Modules.ForEach(m => m.CFG = null);
			TCA_Modules[0].CFG = CFG;
		}
		void UpdateCFG(ShipConstruct ship)
		{ UpdateCFG(ModuleTCA.AllTCA(ship)); }

		bool UpdateEngines(ShipConstruct ship)
		{
			Engines.Clear();
			if(ModuleTCA.HasTCA) 
			{ 
				TCAToolbarManager.SetDefaultButton();
				TCAToolbarManager.ShowButton();
				foreach(Part p in ship.Parts)
					foreach(var module in p.Modules)
					{	
						var engine = module as ModuleEngines;
						if(engine != null) Engines.Add(new EngineWrapper(engine)); 
					}
				if(Engines.Count > 0) return true;
			}
			Reset();
			return false;
		}

		void OnShipModified(ShipConstruct ship) { update_engines = true; }

		bool update_engines, init_engines, reset;
		void Update()
		{
			if(EditorLogic.fetch == null) return;
			if(reset)
			{
				TCAToolbarManager.ShowButton(false);
				Engines.Clear();
				CFG = null;
				reset = false;
			}
			if(init_engines)
			{
				if(UpdateEngines(EditorLogic.fetch.ship)) 
					GetCFG(EditorLogic.fetch.ship);
				else TCAToolbarManager.ShowButton(false);
				init_engines = false;
			}
			if(update_engines)
			{
				if(!UpdateEngines(EditorLogic.fetch.ship)) return;
				if(CFG == null) GetCFG(EditorLogic.fetch.ship);
				else UpdateCFG(EditorLogic.fetch.ship);
				if(CFG != null) CFG.ActiveProfile.Update(Engines);
				update_engines = false;
			}
		}

		protected override void DrawMainWindow(int windowID)
		{
			GUILayout.BeginVertical();
			if(TCAMacroEditor.Editing)
				GUILayout.Label("Edit Macros", Styles.grey, GUILayout.ExpandWidth(true));
			else if(GUILayout.Button("Edit Macros", Styles.normal_button, GUILayout.ExpandWidth(true)))
				TCAMacroEditor.Edit(CFG);
			GUILayout.BeginHorizontal();
			GUILayout.Label("On Launch:", GUILayout.ExpandWidth(false));
			if(Utils.ButtonSwitch("Enable TCA", CFG.Enabled, "", GUILayout.ExpandWidth(false)))
				CFG.Enabled = !CFG.Enabled;
			if(Utils.ButtonSwitch("Hover", CFG.VF[VFlight.AltitudeControl], "Enable Altitude Control", GUILayout.ExpandWidth(false)))
				CFG.VF.Toggle(VFlight.AltitudeControl);
			if(Utils.ButtonSwitch("Follow Terrain", CFG.AltitudeAboveTerrain, "Enable follow terrain mode", GUILayout.ExpandWidth(false)))
				CFG.AltitudeAboveTerrain = !CFG.AltitudeAboveTerrain;
			if(Utils.ButtonSwitch("Use Throttle", CFG.BlockThrottle, "Change altitude/vertical velocity using main throttle control", GUILayout.ExpandWidth(false)))
				CFG.BlockThrottle = !CFG.BlockThrottle;
			if(Utils.ButtonSwitch("VTOL Assist", CFG.VTOLAssistON, "Automatic assistnce with vertical takeof or landing", GUILayout.ExpandWidth(false)))
				CFG.VTOLAssistON = !CFG.VTOLAssistON;
			if(Utils.ButtonSwitch("Flight Stabilizer", CFG.StabilizeFlight, "Automatic flight stabilization when vessel is out of control", GUILayout.ExpandWidth(false)))
				CFG.StabilizeFlight = !CFG.StabilizeFlight;
			GUILayout.EndHorizontal();
			CFG.EnginesProfiles.Draw(height);
			if(CFG.ActiveProfile.Changed)
				CFG.ActiveProfile.Apply(Engines);
			GUILayout.EndVertical();
			base.DrawMainWindow(windowID);

		}

		public void OnGUI()
		{
			if(Engines.Count == 0 || CFG == null || !CFG.GUIVisible || !showHUD) 
			{
				Utils.LockIfMouseOver(LockName, MainWindow, false);
				return;
			}
			Styles.Init();
			Utils.LockIfMouseOver(LockName, MainWindow);
			MainWindow = 
				GUILayout.Window(GetInstanceID(), 
					MainWindow, 
					DrawMainWindow, 
					TCATitle,
					GUILayout.Width(width),
					GUILayout.Height(height));
			MainWindow.clampToScreen();
		}
	}
}