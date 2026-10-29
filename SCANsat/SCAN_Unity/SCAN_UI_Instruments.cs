#region license
/* 
 * [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 *
 * SCAN_UI_Instruments - UI control object for SCANsat instruments readout
 * 
 * Copyright (c)2014 David Grandy <david.grandy@gmail.com>;
 * Copyright (c)2014 technogeeky <technogeeky@gmail.com>;
 * Copyright (c)2014 (Your Name Here) <your email here>; see LICENSE.txt for licensing details.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SCANsat.SCAN_Toolbar;
using SCANsat.Unity.Interfaces;
using SCANsat.Unity.Unity;
using SCANsat.SCAN_Data;
using SCANsat.SCAN_UI.UI_Framework;
using SCANsat.SCAN_PartModules;
using FinePrint;
using FinePrint.Utilities;
using KSP.UI;

namespace SCANsat.SCAN_Unity
{
	public class SCAN_UI_Instruments : ISCAN_Instruments
	{
		private bool _isVisible;
		private bool _mouseInAnomaly;
		private SCANremoteView _anomalyView;
		private SCAN_InstrumentView _instrumentView;
		private SCANtype sensors;
		private SCANdata data;
		private SCANanomaly nearest = null;
		private Vessel v;
		private List<SCANresourceDisplay> resourceScanners = new List<SCANresourceDisplay>();
		private List<SCANresourceGlobal> resources = new List<SCANresourceGlobal>();
		private int currentResource;
		private double degreeOffset;
		private double vlat, vlon;
		private float lastUpdate = 0f;
		private float updateInterval = 0.2f;
		private double maxAnomalyDistance = 20000;
		private double slopeAVG;
		private StringBuilder infoString = new StringBuilder();
		private string slopeString;

		private int oldLines;
		private int lines;

		private float rocUpdateTimer = 2;
		private int activeROCCount;

		private bool showAnomaly = true;

		private Texture anomalyTex;

		private SCAN_Instruments uiElement;

		private static SCAN_UI_Instruments instance;

		public static SCAN_UI_Instruments Instance
		{
			get { return instance; }
		}

		public SCAN_UI_Instruments()
		{
			instance = this;

			data = SCANUtil.getData(FlightGlobals.currentMainBody);

			if (data == null)
			{
				data = new SCANdata(FlightGlobals.currentMainBody);
				SCANcontroller.controller.addToBodyData(FlightGlobals.currentMainBody, data);
			}
			planetConstants(FlightGlobals.currentMainBody);

			v = FlightGlobals.ActiveVessel;
			resources = SCANcontroller.setLoadedResourceList();
			resetResourceList();

			GameEvents.onVesselSOIChanged.Add(soiChange);
			GameEvents.onVesselChange.Add(vesselChange);
			GameEvents.onVesselWasModified.Add(vesselChange);
		}

		private void soiChange(GameEvents.HostedFromToAction<Vessel, CelestialBody> VC)
		{
			data = SCANUtil.getData(VC.to);
			if (data == null)
			{
				data = new SCANdata(VC.to);
				SCANcontroller.controller.addToBodyData(VC.to, data);
			}
			planetConstants(VC.to);
		}

		private bool vesselChanged = false;

		private void vesselChange(Vessel V)
		{
			v = FlightGlobals.ActiveVessel;
			vesselChanged = true;
		}

		public void SetScale(float scale)
		{
			if (uiElement != null)
			{
				uiElement.SetScale(scale);
			}
		}

		public void ProcessTooltips()
		{
			if (uiElement != null)
			{
				uiElement.ProcessTooltips();
			}
		}

		public void Update()
		{
			if (vesselChanged)
			{
				resetResourceList();
				vesselChanged = false;
			}

			if (!_isVisible || uiElement == null)
			{
				return;
			}

			vlat = SCANUtil.fixLatShift(v.latitude);
			vlon = SCANUtil.fixLonShift(v.longitude);

			sensors = SCANcontroller.controller.activeSensorsOnVessel(v.id, true);

			if (SCANUtil.isCovered(vlon, vlat, data, SCANtype.AltimetryLoRes))
			{
				sensors |= SCANtype.AltimetryLoRes;
			}

			if (SCANUtil.isCovered(vlon, vlat, data, SCANtype.AltimetryHiRes))
			{
				sensors |= SCANtype.AltimetryHiRes;
			}

			if (SCANUtil.isCovered(vlon, vlat, data, SCANtype.Biome))
			{
				sensors |= SCANtype.Biome;
			}

			if (SCANUtil.isCovered(vlon, vlat, data, SCANtype.Anomaly))
			{
				sensors |= SCANtype.Anomaly;
			}

			//for (int i = resources.Count - 1; i >= 0; i--)
			//{
			//    if (SCANUtil.isCovered(vlon, vlat, data, resources[i].SType))
			//        sensors |= resources[i].SType;
			//}

			if (SCANUtil.isCovered(vlon, vlat, data, SCANtype.ResourceLoRes))
			{
				sensors |= SCANtype.ResourceLoRes;
			}

			if (SCANUtil.isCovered(vlon, vlat, data, SCANtype.ResourceHiRes))
			{
				sensors |= SCANtype.ResourceHiRes;
			}

			if (SCANcontroller.controller.SerenityLoaded)
			{
				if (Time.realtimeSinceStartup > rocUpdateTimer)
				{
					rocUpdateTimer = Time.realtimeSinceStartup + 2;
					activeROCCount = ROCCount(true);
				}
				else
				{
					activeROCCount = ROCCount(false);
				}
			}

			infoString.Length = 0;

			locationInfo();
			altInfo();
			biomeInfo();
			int resLine = lines;
			resourceInfo();
			anomalyInfo();
			BTDTInfo();
			weatherInfo();
			gravityInfo();
			subsurfaceInfo();
			oceanInfo();
			atmosphereInfo();
			spaceWeatherInfo();

			uiElement.SetAnomalyButtons();

			if (oldLines != resLine)
			{
				oldLines = resLine;

				if (ResourceButtons)
				{
					uiElement.SetResourceButtons(resLine);
				}
			}

			uiElement.UpdateText(infoString.ToString());
		}

		public void OnDestroy()
		{
			if (_instrumentView != null)
			{
				_instrumentView.Free();
				_instrumentView = null;
			}

			if (uiElement != null)
			{
				uiElement.gameObject.SetActive(false);
				MonoBehaviour.Destroy(uiElement.gameObject);
			}

			GameEvents.onVesselSOIChanged.Remove(soiChange);
			GameEvents.onVesselChange.Remove(vesselChange);
			GameEvents.onVesselWasModified.Remove(vesselChange);
		}

		public void Open()
		{
			if (uiElement != null)
			{
				uiElement.gameObject.SetActive(false);
				MonoBehaviour.DestroyImmediate(uiElement.gameObject);
			}

			uiElement = GameObject.Instantiate(SCAN_UI_Loader.InstrumentsPrefab).GetComponent<SCAN_Instruments>();

			if (uiElement == null)
			{
				return;
			}

			uiElement.transform.SetParent(UIMasterController.Instance.dialogCanvas.transform, false);

			_isVisible = true;

			uiElement.SetInstruments(this);

			if (HighLogic.LoadedSceneIsFlight && SCAN_Settings_Config.Instance.StockToolbar && SCAN_Settings_Config.Instance.ToolbarMenu)
			{
				if (SCANappLauncher.Instance != null && SCANappLauncher.Instance.UIElement != null)
				{
					SCANappLauncher.Instance.UIElement.SetInstrumentToggle(true);
				}
			}
		}

		public void Close()
		{
			_isVisible = false;

			oldLines = 0;

			if (uiElement == null)
			{
				return;
			}

			uiElement.FadeOut();

			if (HighLogic.LoadedSceneIsFlight && SCAN_Settings_Config.Instance.StockToolbar && SCAN_Settings_Config.Instance.ToolbarMenu)
			{
				if (SCANappLauncher.Instance != null && SCANappLauncher.Instance.UIElement != null)
				{
					SCANappLauncher.Instance.UIElement.SetInstrumentToggle(false);
				}
			}

			uiElement = null;
		}

		public string Version
		{
			get { return SCANmainMenuLoader.SCANsatVersion; }
		}

		public string Readout
		{
			get { return infoString.ToString(); }
		}

		public string ResourceName
		{
			get { return resources[currentResource].DisplayName; }
		}

		public string TypingText
		{
			get { return ""; }
		}

		public string AnomalyText
		{
			get { return ""; }
		}

		public bool IsVisible
		{
			get { return _isVisible; }
			set
			{
				_isVisible = value;

				if (!value)
				{
					Close();
					infoString.Release();
				}
			}
		}

		public bool ResourceButtons
		{
			get { return SCANcontroller.MasterResourceCount > 1; }
		}

		public bool AnomalyButtons
		{
			get
			{
				if (_instrumentView != null && _instrumentView.IsActive && _instrumentView.AvailableInstrumentCount > 1)
				{
					return true;
				}

				if (!SCANcontroller.controller.SerenityLoaded)
				{
					return false;
				}

				if (data == null)
				{
					return false;
				}

				if (nearest == null)
				{
					return false;
				}

				return activeROCCount > 0;
			}
		}

		private int ROCCount(bool update)
		{
			if (data == null)
			{
				return 0;
			}

			List<SCANROC> rocs = data.ROCS(update);

			if (rocs == null)
			{
				return 0;
			}

			int c = 0;

			for (int i = rocs.Count - 1; i >= 0; i--)
			{
				if (rocs[i].Known)
				{
					c++;
				}
			}

			return c;
		}

		public bool MouseAnomaly
		{
			get { return _mouseInAnomaly; }
			set { _mouseInAnomaly = value; }
		}

		public bool TooltipsOn
		{
			get { return SCAN_Settings_Config.Instance.WindowTooltips; }
		}

		public float Scale
		{
			get { return SCAN_Settings_Config.Instance.UIScale; }
		}

		public Texture AnomalyCamera
		{
			get { return null; }
		}

		public Canvas TooltipCanvas
		{
			get { return UIMasterController.Instance.tooltipCanvas; }
		}

		public Vector2 Position
		{
			get { return SCAN_Settings_Config.Instance.InstrumentsPosition; }
			set { SCAN_Settings_Config.Instance.InstrumentsPosition = value; }
		}

		public void ClampToScreen(RectTransform rect)
		{
			UIMasterController.ClampToScreen(rect, Vector2.zero);
		}

		public void NextResource()
		{
			currentResource += 1;

			if (currentResource >= resources.Count)
			{
				currentResource = 0;
			}
		}

		public void PreviousResource()
		{
			currentResource -= 1;

			if (currentResource < 0)
			{
				currentResource = resources.Count - 1;
			}
		}

		public void NextAnomaly()
		{
			if (_instrumentView != null && _instrumentView.IsActive && _instrumentView.AvailableInstrumentCount > 1)
			{
				_instrumentView.NextMode();
				return;
			}

			showAnomaly = !showAnomaly;
		}

		private void locationInfo()
		{
			infoString.AppendFormat("Lat: {0}°, Lon: {1}°", vlat.ToString("F2"), vlon.ToString("F2"));

			lines = 1;

			for (int i = data.Waypoints.Count - 1; i >= 0; i--)
			{
				SCANwaypoint p = data.Waypoints[i];

				if (p.LandingTarget)
				{
					double distance = SCANUtil.waypointDistance(vlat, vlon, v.altitude, p.Latitude, p.Longitude, v.altitude, data.Body);
					if (distance <= 15000)
					{
						infoString.AppendLine();
						infoString.AppendFormat("Target Dist: {0}m", distance.ToString("N1"));
						lines++;
					}
					continue;
				}

				if (p.Band == FlightBand.NONE)
				{
					continue;
				}

				if (p.Root != null)
				{
					if (p.Root.ContractState != Contracts.Contract.State.Active)
					{
						continue;
					}
				}

				if (p.Param != null)
				{
					if (p.Param.State != Contracts.ParameterState.Incomplete)
					{
						continue;
					}
				}

				double range = waypointRange(p);

				if (SCANUtil.waypointDistance(vlat, vlon, v.altitude, p.Latitude, p.Longitude, v.altitude, data.Body) <= range)
				{
					infoString.AppendLine();
					infoString.AppendFormat("Waypoint: {0}", p.Name);
					lines++;
					break;
				}
			}
		}

		private void altInfo()
		{
			double h = v.altitude;
			double pqs = 0;
			if (v.mainBody.pqsController != null)
			{
				pqs = v.PQSAltitude();
				if (pqs > 0 || !v.mainBody.ocean)
				{
					h -= pqs;
				}
			}
			if (h < 0)
			{
				h = v.altitude;
			}

			bool drawSlope = false;

			switch (v.situation)
			{
				case Vessel.Situations.LANDED:
				case Vessel.Situations.PRELAUNCH:
					infoString.AppendLine();
					infoString.AppendFormat("Terrain: {0}m", pqs.ToString("N1"));
					lines++;
					drawSlope = true;
					break;
				case Vessel.Situations.SPLASHED:
					double d = Math.Abs(pqs) - Math.Abs(h);
					if ((sensors & SCANtype.Altimetry) != SCANtype.Nothing)
					{
						infoString.AppendLine();
						infoString.AppendFormat("Depth: {0}", SCANuiUtil.distanceString(Math.Abs(d), 10000));
						lines++;
					}
					else
					{
						d = ((int)(d / 100)) * 100;
						infoString.AppendLine();
						infoString.AppendFormat("Depth: {0}", SCANuiUtil.distanceString(Math.Abs(d), 10000));
						lines++;
					}
					drawSlope = false;
					break;
				default:
					if (h < 1000 || (sensors & SCANtype.AltimetryHiRes) != SCANtype.Nothing)
					{
						infoString.AppendLine();
						infoString.AppendFormat("Altitude: {0}", SCANuiUtil.distanceString(h, 100000));
						lines++;
						drawSlope = true;
					}
					else if ((sensors & SCANtype.AltimetryLoRes) != SCANtype.Nothing)
					{
						h = ((int)(h / 500)) * 500;
						infoString.AppendLine();
						infoString.AppendFormat("Altitude: {0}", SCANuiUtil.distanceString(h, 100000));
						lines++;
						drawSlope = false;
					}
					break;
			}

			if (drawSlope)
			{
				//Calculate slope less frequently; the rapidly changing value makes it difficult to read otherwise
				if (v.mainBody.pqsController != null)
				{
					float deltaTime = 1f;
					if (Time.deltaTime != 0)
					{
						deltaTime = TimeWarp.deltaTime / Time.deltaTime;
					}

					if (deltaTime > 5)
					{
						deltaTime = 5;
					}

					if (((Time.time * deltaTime) - lastUpdate) > updateInterval)
					{
						lastUpdate = Time.time;

						slopeAVG = SCANUtil.slope(pqs, v.mainBody, vlon, vlat, degreeOffset);
						slopeString = string.Format("Slope: {0}°", slopeAVG.ToString("F2"));
					}

					infoString.AppendLine();
					infoString.Append(slopeString);
					lines++;
				}
			}
		}

		private void biomeInfo()
		{
			if ((sensors & SCANtype.Biome) != SCANtype.Nothing && v.mainBody.BiomeMap != null)
			{
				infoString.AppendLine();
				infoString.AppendFormat("Biome: {0}", SCANUtil.getBiomeDisplayName(v.mainBody, vlon, vlat));
				lines++;
			}
		}

		private void resourceInfo()
		{
			if (v.mainBody.pqsController == null)
			{
				return;
			}

			if (SCAN_Settings_Config.Instance.RequireNarrowBand)
			{
				bool tooHigh = false;
				bool scanner = false;

				for (int i = resourceScanners.Count - 1; i >= 0; i--)
				{
					SCANresourceDisplay s = resourceScanners[i];

					if (s == null)
					{
						continue;
					}

					if (s.ResourceName != resources[currentResource].Name)
					{
						continue;
					}

					if (ResourceUtilities.GetAltitude(v) > s.MaxAbundanceAltitude && !v.Landed)
					{
						tooHigh = true;
						continue;
					}

					scanner = true;
					tooHigh = false;
					break;
				}

				resourceLabel(resources[currentResource], tooHigh, scanner);
			}
			else
			{
				resourceLabel(resources[currentResource], false, true);
			}
		}

		private void resourceLabel(SCANresourceGlobal r, bool high, bool onboard)
		{
			if ((sensors & SCANtype.ResourceHiRes) != SCANtype.Nothing)
			{
				if (high || !onboard)
				{
					infoString.AppendLine();
					infoString.AppendFormat("{0}: {1}", r.DisplayName, SCANUtil.ResourceOverlay(vlat, vlon, r.Name, v.mainBody, SCAN_Settings_Config.Instance.BiomeLock).ToString("P0"));
				}
				else
				{
					infoString.AppendLine();
					infoString.AppendFormat("{0}: {1}", r.DisplayName, SCANUtil.ResourceOverlay(vlat, vlon, r.Name, v.mainBody, SCAN_Settings_Config.Instance.BiomeLock).ToString("P2"));
				}
			}
			else if ((sensors & SCANtype.ResourceLoRes) != SCANtype.Nothing)
			{
				infoString.AppendLine();
				infoString.AppendFormat("{0}: {1}", r.DisplayName, SCANuiUtil.LoResourceGroup(SCANUtil.ResourceOverlay(vlat, vlon, r.Name, v.mainBody, SCAN_Settings_Config.Instance.BiomeLock)));
			}
			else if (ResourceButtons)
			{
				infoString.AppendLine();
				infoString.AppendFormat("{0}: No Data", r.DisplayName);
			}
		}

		private void anomalyInfo()
		{
			nearest = null;
			double nearest_dist = -1;

			for (int i = data.Anomalies.Length - 1; i >= 0; i--)
			{
				SCANanomaly a = data.Anomalies[i];

				if (!a.Known && !a.Detail)
				{
					continue;
				}

				double d = (a.Mod.transform.position - v.transform.position).magnitude;

				if (d < nearest_dist || nearest_dist < 0)
				{
					if (d < maxAnomalyDistance)
					{
						nearest = a;
						nearest_dist = d;
					}
				}
			}

			if ((showAnomaly || activeROCCount <= 0) && nearest != null)
			{
				infoString.AppendLine();

				if (nearest.Detail && nearest.Known)
				{
					infoString.Append(string.Format("{0}: {1}", nearest.Name, SCANuiUtil.distanceString(nearest_dist, 2000)));
				}
				else
				{
					if (nearest.Detail)
					{
						infoString.Append(nearest.Name);
					}
					else if (nearest.Known)
					{
						infoString.Append(string.Format("Unknown Anomaly: {0}", SCANuiUtil.distanceString(nearest_dist, 2000)));
					}
				}
			}
			else if (SCANcontroller.controller.SerenityLoaded && activeROCCount > 0)
			{
				infoString.AppendLine();

				infoString.AppendFormat("{0} Features Detected", activeROCCount.ToString());
			}
		}

		private bool showScientificInstrumentView()
		{
			if (_instrumentView == null)
			{
				_instrumentView = new SCAN_InstrumentView();
			}

			if (_instrumentView.Update(v))
			{
				uiElement.SetDetailState(true);
				anomalyTex = _instrumentView.GetTexture();
				uiElement.UpdateAnomaly(anomalyTex);
				uiElement.UpdateAnomalyName(_instrumentView.GetNameString());
				uiElement.UpdateAnomalyText(_instrumentView.GetInfoString());
				return true;
			}

			return false;
		}

		private void BTDTInfo()
		{
			if ((sensors & SCANtype.AnomalyDetail) == SCANtype.Nothing)
			{
				if (showScientificInstrumentView())
				{
					if (_anomalyView != null)
					{
						_anomalyView.free();
						_anomalyView = null;
					}
					return;
				}

				uiElement.SetDetailState(false);

				if (_anomalyView != null)
				{
					_anomalyView.free();
				}

				_anomalyView = null;

				return;
			}

			if (activeROCCount <= 0 && (nearest == null || !nearest.Detail || nearest.Mod == null))
			{
				if (showScientificInstrumentView())
				{
					if (_anomalyView != null)
					{
						_anomalyView.free();
						_anomalyView = null;
					}
					return;
				}

				uiElement.SetDetailState(false);

				if (_anomalyView != null)
				{
					_anomalyView.free();
				}

				_anomalyView = null;

				return;
			}

			if ((showAnomaly || activeROCCount <= 0) && nearest != null)
			{
				if (_anomalyView == null)
				{
					_anomalyView = new SCANremoteView();
					_anomalyView.Initialize(320, 240);
					_anomalyView.setup(nearest.Mod.gameObject);
				}

				if (!_anomalyView.valid(nearest.Mod.gameObject))
				{
					uiElement.SetDetailState(false);

					if (_anomalyView != null)
					{
						_anomalyView.free();
					}

					_anomalyView = null;

					return;
				}
			}
			else if (SCANcontroller.controller.SerenityLoaded && activeROCCount > 0)
			{
				if (_anomalyView == null)
				{
					_anomalyView = new SCANremoteView();
					_anomalyView.Initialize(320, 240);
					_anomalyView.setup(data.ROCS(true), v);
				}

				if (!_anomalyView.validROC())
				{
					uiElement.SetDetailState(false);

					if (_anomalyView != null)
					{
						_anomalyView.free();
					}

					_anomalyView = null;

					return;
				}
			}
			else
			{
				uiElement.SetDetailState(false);

				if (_anomalyView != null)
				{
					_anomalyView.free();
				}

				_anomalyView = null;

				return;
			}

			uiElement.SetDetailState(true);

			anomalyTex = _anomalyView.getTexture();

			uiElement.UpdateAnomaly(anomalyTex);

			uiElement.UpdateAnomalyText(_anomalyView.getInfoString());

			if ((showAnomaly || activeROCCount <= 0) && nearest != null)
			{
				uiElement.UpdateAnomalyName(_anomalyView.getAnomalyDataString(_mouseInAnomaly, nearest.Known));
			}
			else
			{
				uiElement.UpdateAnomalyName(_anomalyView.getAnomalyDataString(_mouseInAnomaly, false));
			}
		}

		private string distanceString(double dist)
		{
			if (dist < 5000)
			{
				return string.Format("{0}m", dist.ToString("N1"));
			}

			return string.Format("{0}km", (dist / 1000).ToString("N3"));
		}

		private double waypointRange(SCANwaypoint p)
		{
			double min = ContractDefs.Survey.MinimumTriggerRange;
			double max = ContractDefs.Survey.MaximumTriggerRange;

			switch (p.Band)
			{
				case FlightBand.GROUND:
					return min;
				case FlightBand.LOW:
					return (min + max) / 2;
				case FlightBand.HIGH:
					return max;
				default:
					return max;
			}
		}

		private void planetConstants(CelestialBody b)
		{
			double circum = b.Radius * 2 * Math.PI;
			double eqDistancePerDegree = circum / 360;
			degreeOffset = 5 / eqDistancePerDegree;
		}

		public void resetResourceList()
		{
			resourceScanners = new List<SCANresourceDisplay>();

			if (v == null)
			{
				return;
			}

			foreach (SCANresourceDisplay s in v.FindPartModulesImplementing<SCANresourceDisplay>())
			{
				if (s == null)
				{
					continue;
				}

				if (resourceScanners.Contains(s))
				{
					continue;
				}

				resourceScanners.Add(s);
			}
		}

		public void ResetPosition()
		{
			SCAN_Settings_Config.Instance.InstrumentsPosition = new Vector2(100, -500);

			if (uiElement != null)
			{
				uiElement.SetPosition(SCAN_Settings_Config.Instance.InstrumentsPosition);
			}
		}
	
		private void weatherInfo()
		{
			if (v == null) return;
			SCANweather weather = v.FindPartModuleImplementing<SCANweather>();
			if (weather == null) return;

			infoString.AppendLine();
			infoString.Append("<color=#00e5ff><b>[气象监测 / Meteorology]</b></color>");
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 云量: {0:F0}% | 降水: {1:F1} mm/h", weather.currentCloudCover * 100f, weather.currentPrecipRate);
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 风场: {0:F1} m/s ({1}) | 气压: {2:F0} hPa", weather.currentWindSpeed, weather.currentWindCardinal, weather.currentMslp);
			lines++;

			if (weather.currentSst > 0)
			{
				infoString.AppendLine();
				infoString.AppendFormat(" 海温(SST): {0:F1} K ({1:F1}°C) | 辐射: {2:F0} W/m²", weather.currentSst, weather.currentSst - 273.15f, weather.currentOlr);
				lines++;
			}
		}

		private void gravityInfo()
		{
			if (v == null) return;
			SCANgravity grav = v.FindPartModuleImplementing<SCANgravity>();
			if (grav == null) return;

			infoString.AppendLine();
			infoString.Append("<color=#ffea00><b>[引力与大地测量 / Gravimetry]</b></color>");
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 自由空气重力异常: {0:+0.0;-0.0;0.0} mGal", grav.currentFreeAirMGal);
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 布格异常: {0:+0.0;-0.0;0.0} mGal | 重力加速度: {1:F3} m/s²", grav.currentBouguerMGal, grav.currentG);
			lines++;

			if (!string.IsNullOrEmpty(grav.currentMasconName) && grav.currentMasconName != "无显著异常" && grav.currentMasconName != "None")
			{
				infoString.AppendLine();
				infoString.AppendFormat(" <color=#ff5252>★ 质量密集区(Mascon)警报: {0}</color>", grav.currentMasconName);
				lines++;
			}
		}

		private void subsurfaceInfo()
		{
			if (v == null) return;
			SCANsubsurface sub = v.FindPartModuleImplementing<SCANsubsurface>();
			if (sub == null) return;

			infoString.AppendLine();
			infoString.Append("<color=#b388ff><b>[次表层穿透雷达 / Subsurface GPR]</b></color>");
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 风化层厚度: {0:F1} m | 基岩埋深: {1:F0} m", sub.regolithThickness, sub.bedrockDepth);
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 介电常数: ε = {0:F1}", sub.dielectricConstant);
			lines++;

			if (!string.IsNullOrEmpty(sub.detectedFeature) && sub.detectedFeature != "连续层理" && sub.detectedFeature != "None")
			{
				infoString.AppendLine();
				infoString.AppendFormat(" <color=#40c4ff>◆ 探明地质构造: {0}</color>", sub.detectedFeature);
				lines++;
			}
		}

		private void oceanInfo()
		{
			if (v == null) return;
			SCANocean ocean = v.FindPartModuleImplementing<SCANocean>();
			if (ocean == null) return;

			infoString.AppendLine();
			infoString.Append("<color=#00b0ff><b>[海洋动力与水色 / Oceanography]</b></color>");
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 有效波高(SWH): {0:F2} m ({1})", ocean.swh, ocean.seaStateDesc);
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 叶绿素-a: {0:F2} mg/m³ | 盐度: {1:F1} PSU", ocean.chlorophyll, ocean.salinity);
			lines++;
		}

		private void atmosphereInfo()
		{
			if (v == null) return;
			SCANatmosphere atmo = v.FindPartModuleImplementing<SCANatmosphere>();
			if (atmo == null) return;

			infoString.AppendLine();
			infoString.Append("<color=#69f0ae><b>[温室气体与碳汇 / GHG & Carbon]</b></color>");
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" XCO₂: {0:F1} ppm | XCH₄: {1:F0} ppb", atmo.xco2, atmo.xch4);
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 气溶胶厚度(AOD): {0:F3} | 动态: {1}", atmo.aod, atmo.carbonFluxType);
			lines++;
		}

		private void spaceWeatherInfo()
		{
			if (v == null) return;
			SCANspaceWeather sw = v.FindPartModuleImplementing<SCANspaceWeather>();
			if (sw == null) return;

			infoString.AppendLine();
			infoString.Append("<color=#ff4081><b>[空间天气与磁层 / Space Weather]</b></color>");
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 总磁场强度 |B|: {0:F1} nT | TEC: {1:F1} TECU", sw.magneticField_nT, sw.tecUnits);
			lines++;

			infoString.AppendLine();
			infoString.AppendFormat(" 质子通量: {0:F1} p/cm²/s | 磁暴等级: {1} ({2})", sw.protonFlux, sw.stormLevelDesc, sw.kpIndexStr);
			lines++;
		}

	}
}
