#region license
/*
 * [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 * 
 * SCANweather - Meteorological satellite telemetry & KerbalWeatherProject (KWP) bridge
 */
#endregion

using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace SCANsat.SCAN_PartModules
{
	public class SCANweather : PartModule
	{
		[KSPField(isPersistant = true, guiActive = true, guiName = "气象遥测")]
		public string weatherStatus = "已联机";

		[KSPField(isPersistant = false, guiActive = true, guiName = "云系状态", groupName = "WeatherTelemetry", groupDisplayName = "🛰️ 气象卫星空间遥测")]
		public string cloudInfo = "待机";

		[KSPField(isPersistant = false, guiActive = true, guiName = "降水水汽", groupName = "WeatherTelemetry", groupDisplayName = "🛰️ 气象卫星空间遥测")]
		public string precipInfo = "待机";

		[KSPField(isPersistant = false, guiActive = true, guiName = "海风风场", groupName = "WeatherTelemetry", groupDisplayName = "🛰️ 气象卫星空间遥测")]
		public string windInfo = "待机";

		[KSPField(isPersistant = false, guiActive = true, guiName = "海温气压", groupName = "WeatherTelemetry", groupDisplayName = "🛰️ 气象卫星空间遥测")]
		public string surfaceInfo = "待机";

		[KSPField(isPersistant = false, guiActive = true, guiName = "出射辐射(OLR)", groupName = "WeatherTelemetry", groupDisplayName = "🛰️ 气象卫星空间遥测")]
		public string olrInfo = "待机";

		[KSPField]
		public string weatherCapabilities = "All"; // All, Optical, Precipitation, Wind, Sounding

		[KSPField]
		public float updateInterval = 2.0f;

		private float lastUpdate = 0f;

		// Telemetry Cache
		public double currentCloudCover;
		public double currentCloudTopTemp;
		public double currentPrecipWater;
		public double currentPrecipRate;
		public double currentMslp;
		public double currentSst;
		public double currentWindSpeed;
		public double currentWindDir;
		public string currentWindCardinal = "N";
		public double currentOlr;
		public double currentTemp;
		public bool isKwpActive = false;

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			if (!HighLogic.LoadedSceneIsFlight) return;
			KWPBridge.Initialize();
			updateTelemetry(true);
		}

		public void Update()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
			if (Time.time - lastUpdate < updateInterval) return;
			lastUpdate = Time.time;

			updateTelemetry(false);
		}

		private void updateTelemetry(bool force)
		{
			CelestialBody body = vessel.mainBody;
			if (body == null) return;

			if (!body.atmosphere)
			{
				weatherStatus = "无大气层天体";
				cloudInfo = "真空环境无云层";
				precipInfo = "无降水";
				windInfo = "真空无气流";
				surfaceInfo = "无海表气压";
				olrInfo = "黑体辐射";
				return;
			}

			weatherStatus = KWPBridge.IsAvailable ? "KWP 实时流体模型" : "大气物理推算";

			double lat = vessel.latitude;
			double lon = vessel.longitude;
			double ut = Planetarium.GetUniversalTime();

			if (KWPBridge.IsAvailable)
			{
				try
				{
					currentCloudCover = KWPBridge.GetTotalCloudCover(lat, lon, ut);
					currentOlr = KWPBridge.GetOLR(lat, lon, ut);
					currentCloudTopTemp = KWPBridge.GetCloudTopTemp(currentOlr);
					currentPrecipWater = KWPBridge.GetPrecipitableWater(lat, lon, ut);
					currentPrecipRate = KWPBridge.GetPrecipRate(lat, lon, ut);
					currentMslp = KWPBridge.GetMSLP(lat, lon, ut);
					currentSst = KWPBridge.GetSST(lat, lon, ut);
					currentWindSpeed = KWPBridge.GetWindSpeed(lat, lon, 0, ut, out currentWindDir, out currentWindCardinal);
					currentTemp = KWPBridge.GetTemperature(lat, lon, 0, ut);
					isKwpActive = true;
				}
				catch
				{
					isKwpActive = false;
					fallbackWeatherEstimation(lat, lon, body);
				}
			}
			else
			{
				isKwpActive = false;
				fallbackWeatherEstimation(lat, lon, body);
			}

			// Format PAW strings based on capabilities
			cloudInfo = string.Format("{0:F1}% (云顶 {1:F1}°C)", currentCloudCover * 100.0, currentCloudTopTemp);

			if (currentPrecipRate > 0.05)
			{
				string rainDesc = currentPrecipRate > 8.0 ? "强暴雨" : (currentPrecipRate > 2.5 ? "中雨" : "小雨/毛毛雨");
				precipInfo = string.Format("{0:F1} mm/h ({1}) | 水汽: {2:F1}mm", currentPrecipRate, rainDesc, currentPrecipWater);
			}
			else
			{
				precipInfo = string.Format("无降水 | 水汽柱: {0:F1}mm", currentPrecipWater);
			}

			windInfo = string.Format("{0:F1} m/s ({1:F0}° {2})", currentWindSpeed, currentWindDir, currentWindCardinal);

			string sstStr = body.ocean ? string.Format("{0:F1}°C", currentSst) : "内陆无海表";
			surfaceInfo = string.Format("海温: {0} | 气压: {1:F1} hPa", sstStr, currentMslp);

			olrInfo = string.Format("{0:F1} W/m²", currentOlr);
		}

		private void fallbackWeatherEstimation(double lat, double lon, CelestialBody body)
		{
			// High-realism atmospheric fallback model when KWP is not present
			double absLat = Math.Abs(lat);
			double seasonalShift = Math.Sin(Planetarium.GetUniversalTime() / (body.orbit.period > 0 ? body.orbit.period : 86400 * 365) * 2 * Math.PI);
			double effLat = absLat - seasonalShift * 10.0;
			if (effLat < 0) effLat = 0;

			// Intertropical Convergence Zone (ITCZ) & mid-latitude storm tracks
			double itczFactor = Math.Exp(-Math.Pow(effLat / 12.0, 2));
			double stormTrackFactor = Math.Exp(-Math.Pow((effLat - 48.0) / 14.0, 2));
			double desertFactor = Math.Exp(-Math.Pow((effLat - 25.0) / 10.0, 2));

			// Pseudo-random local weather wave based on longitude & time
			double weatherWave = Math.Sin(lon * Math.PI / 60.0 + Planetarium.GetUniversalTime() / 7200.0);
			double cloudRaw = 0.35 + 0.40 * itczFactor + 0.35 * stormTrackFactor - 0.25 * desertFactor + 0.20 * weatherWave;
			currentCloudCover = Math.Max(0.02, Math.Min(0.98, cloudRaw));

			currentOlr = 260.0 - currentCloudCover * 90.0;
			currentCloudTopTemp = -15.0 - currentCloudCover * 45.0; // -15C to -60C
			currentPrecipWater = Math.Max(2.0, (1.0 - absLat / 90.0) * 45.0 + weatherWave * 8.0);
			currentPrecipRate = currentCloudCover > 0.65 ? (currentCloudCover - 0.65) * 16.0 : 0.0;

			currentMslp = 1013.25 + Math.Sin(effLat * Math.PI / 30.0) * 12.0 - (currentPrecipRate > 5.0 ? 15.0 : 0.0);
			currentSst = Math.Max(-2.0, (1.0 - Math.Pow(absLat / 75.0, 2)) * 28.0);

			// Trade winds & Westerlies
			double baseWind = (effLat > 35.0 && effLat < 65.0) ? 12.0 : 6.0;
			currentWindSpeed = baseWind + Math.Abs(weatherWave) * 8.0;
			currentWindDir = (effLat > 35.0 && effLat < 65.0) ? 250.0 + weatherWave * 30.0 : 70.0 + weatherWave * 30.0;
			if (currentWindDir < 0) currentWindDir += 360.0;
			if (currentWindDir >= 360.0) currentWindDir -= 360.0;
			currentWindCardinal = getCardinalDirection(currentWindDir);
			currentTemp = currentSst;
		}

		private static string getCardinalDirection(double deg)
		{
			string[] cardinals = { "北(N)", "东北偏北(NNE)", "东北(NE)", "东北偏东(ENE)", "东(E)", "东南偏东(ESE)", "东南(SE)", "东南偏南(SSE)",
								   "南(S)", "西南偏南(SSW)", "西南(SW)", "西南偏西(WSW)", "西(W)", "西北偏西(WNW)", "西北(NW)", "西北偏北(NNW)" };
			int idx = (int)Math.Round(((deg % 360.0) / 22.5)) % 16;
			if (idx < 0) idx += 16;
			return cardinals[idx];
		}

		[KSPEvent(guiActive = true, guiName = "📊 生成实时气象遥测简报", groupName = "WeatherTelemetry")]
		public void ShowWeatherBriefing()
		{
			updateTelemetry(true);

			StringBuilder sb = new StringBuilder();
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine(string.Format("🛰️ 载荷型号: {0} ({1})", part.partInfo.title, weatherCapabilities));
			sb.AppendLine(string.Format("🌍 目标天体: {0} | 轨道高度: {1:N1} km", vessel.mainBody.bodyName, vessel.altitude / 1000.0));
			sb.AppendLine(string.Format("📍 星下点位置: {0:F2}°{1}, {2:F2}°{3}",
				Math.Abs(vessel.latitude), vessel.latitude >= 0 ? "N" : "S",
				Math.Abs(vessel.longitude), vessel.longitude >= 0 ? "E" : "W"));
			sb.AppendLine(string.Format("📡 数据源状态: {0}", isKwpActive ? "KerbalWeatherProject 全球大气流体格点" : "标准空间对流层辐射推算模型"));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine("【云系分布与热力学状态】");
			sb.AppendLine(string.Format(" • 总云量覆盖 (Total Cloud Cover):   {0:F1} %", currentCloudCover * 100.0));
			sb.AppendLine(string.Format(" • 云顶相当黑体温度 (Cloud Top Temp): {0:F1} °C", currentCloudTopTemp));
			sb.AppendLine(string.Format(" • 出射长波辐射 (OLR):               {0:F1} W/m²", currentOlr));
			sb.AppendLine("【水文循环与降水监测】");
			sb.AppendLine(string.Format(" • 整层大气可降水量 (PWV):            {0:F1} mm", currentPrecipWater));
			sb.AppendLine(string.Format(" • 地表瞬时降水速率 (Precip Rate):    {0:F2} mm/h", currentPrecipRate));
			sb.AppendLine("【海洋环境与表面风场】");
			sb.AppendLine(string.Format(" • 海平面气压 (MSLP):                {0:F1} hPa", currentMslp));
			sb.AppendLine(string.Format(" • 海面表面温度 (SST):                {0:F1} °C", currentSst));
			sb.AppendLine(string.Format(" • 10米海面风速与风向:                {0:F1} m/s ({1:F0}° {2})", currentWindSpeed, currentWindDir, currentWindCardinal));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

			if (currentWindSpeed >= 17.2 || currentPrecipRate >= 10.0 || currentCloudTopTemp <= -52.0)
			{
				sb.AppendLine("⚠️【重要气象告警】");
				if (currentWindSpeed >= 17.2) sb.AppendLine(" • 探测到热带风暴/大风强风圈！海面波浪剧烈！");
				if (currentPrecipRate >= 10.0) sb.AppendLine(" • 探测到剧烈短时强降水中心（暴雨云团）！");
				if (currentCloudTopTemp <= -52.0) sb.AppendLine(" • 探测到强对流深厚云系发展（对流层顶冲顶云冠）！");
				sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			}

			PopupDialog.SpawnPopupDialog(
				new Vector2(0.5f, 0.5f),
				new Vector2(0.5f, 0.5f),
				new MultiOptionDialog(
					"SCANsatWeatherBriefing",
					sb.ToString(),
					"🛰️ 空间气象遥测与对流层实时监测报表",
					HighLogic.UISkin,
					new DialogGUIButton("关闭", null, true)
				),
				false,
				HighLogic.UISkin
			);
		}
	
			[KSPEvent(guiActive = true, guiName = "查看气象云图与科学读数", active = true)]
		public void OpenInstrumentsWindow()
		{
			if (global::SCANsat.SCAN_Unity.SCAN_UI_Instruments.Instance != null && !global::SCANsat.SCAN_Unity.SCAN_UI_Instruments.Instance.IsVisible)
			{
				global::SCANsat.SCAN_Unity.SCAN_UI_Instruments.Instance.Open();
			}
		}
	}

	/// <summary>
	/// Dynamic reflection bridge to KerbalWeatherProject (KWP)
	/// Ensures zero crash risk if KWP is not installed.
	/// </summary>


	public static class KWPBridge
	{
		public static bool IsAvailable { get; private set; }

		private static MethodInfo miTotalCloudCover;
		private static MethodInfo miCloudTopTemps;
		private static MethodInfo miOLR;
		private static MethodInfo miPrecitableWater;
		private static MethodInfo miPrate;
		private static MethodInfo miMslp;
		private static MethodInfo miSst;
		private static MethodInfo miUwind;
		private static MethodInfo miVwind;
		private static MethodInfo miWspd;
		private static MethodInfo miWdirDegrees;
		private static MethodInfo miWdirCardinal;
		private static MethodInfo miTemperature;
		private static MethodInfo miPressure;
		private static MethodInfo miRelativeHumidity;

		private static bool initialized = false;

		public static void Initialize()
		{
			if (initialized) return;
			initialized = true;

			try
			{
				Assembly kwpAsm = null;
				foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
				{
					if (a.GetName().Name == "KerbalWeatherProject")
					{
						kwpAsm = a;
						break;
					}
				}

				if (kwpAsm == null)
				{
					IsAvailable = false;
					return;
				}

				Type climApi = kwpAsm.GetType("KerbalWeatherProject.climate_api");
				if (climApi == null)
				{
					IsAvailable = false;
					return;
				}

				Type dbl = typeof(double);
				miTotalCloudCover = climApi.GetMethod("total_cloud_cover", new Type[] { dbl, dbl, dbl });
				miOLR = climApi.GetMethod("OLR", new Type[] { dbl, dbl, dbl });
				miCloudTopTemps = climApi.GetMethod("cloud_top_temps", new Type[] { dbl });
				miPrecitableWater = climApi.GetMethod("precitable_water", new Type[] { dbl, dbl, dbl });
				miPrate = climApi.GetMethod("prate", new Type[] { dbl, dbl, dbl });
				miMslp = climApi.GetMethod("mslp", new Type[] { dbl, dbl, dbl });
				miSst = climApi.GetMethod("sst", new Type[] { dbl, dbl, dbl });
				miUwind = climApi.GetMethod("uwind", new Type[] { dbl, dbl, dbl, dbl });
				miVwind = climApi.GetMethod("vwind", new Type[] { dbl, dbl, dbl, dbl });
				miWspd = climApi.GetMethod("wspd", new Type[] { dbl, dbl, dbl });
				miWdirDegrees = climApi.GetMethod("wdir_degrees", new Type[] { dbl, dbl });
				miWdirCardinal = climApi.GetMethod("wdir_cardinal", new Type[] { dbl });
				miTemperature = climApi.GetMethod("temperature", new Type[] { dbl, dbl, dbl, dbl });
				miPressure = climApi.GetMethod("pressure", new Type[] { dbl, dbl, dbl, dbl });
				miRelativeHumidity = climApi.GetMethod("relative_humidity", new Type[] { dbl, dbl, dbl, dbl });

				IsAvailable = (miTotalCloudCover != null && miOLR != null);
				if (IsAvailable)
				{
					Debug.Log("[SCANsat] Successfully bridged with KerbalWeatherProject (KWP) climate_api!");
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning("[SCANsat] Failed to initialize KWP bridge: " + e.Message);
				IsAvailable = false;
			}
		}

		public static double GetTotalCloudCover(double lat, double lon, double ut)
		{
			if (miTotalCloudCover == null) return 0.0;
			return (double)miTotalCloudCover.Invoke(null, new object[] { lat, lon, ut });
		}

		public static double GetOLR(double lat, double lon, double ut)
		{
			if (miOLR == null) return 240.0;
			return (double)miOLR.Invoke(null, new object[] { lat, lon, ut });
		}

		public static double GetCloudTopTemp(double olr)
		{
			if (miCloudTopTemps == null) return -20.0;
			return (double)miCloudTopTemps.Invoke(null, new object[] { olr });
		}

		public static double GetPrecipitableWater(double lat, double lon, double ut)
		{
			if (miPrecitableWater == null) return 15.0;
			return (double)miPrecitableWater.Invoke(null, new object[] { lat, lon, ut });
		}

		public static double GetPrecipRate(double lat, double lon, double ut)
		{
			if (miPrate == null) return 0.0;
			return (double)miPrate.Invoke(null, new object[] { lat, lon, ut });
		}

		public static double GetMSLP(double lat, double lon, double ut)
		{
			if (miMslp == null) return 1013.25;
			return (double)miMslp.Invoke(null, new object[] { lat, lon, ut });
		}

		public static double GetSST(double lat, double lon, double ut)
		{
			if (miSst == null) return 15.0;
			return (double)miSst.Invoke(null, new object[] { lat, lon, ut });
		}

		public static double GetTemperature(double lat, double lon, double alt, double ut)
		{
			if (miTemperature == null) return 15.0;
			return (double)miTemperature.Invoke(null, new object[] { lat, lon, alt, ut });
		}

		public static double GetWindSpeed(double lat, double lon, double alt, double ut, out double dirDeg, out string cardinal)
		{
			dirDeg = 0;
			cardinal = "N";
			if (miUwind == null || miVwind == null || miWspd == null) return 0.0;

			double u = (double)miUwind.Invoke(null, new object[] { lat, lon, alt, ut });
			double v = (double)miVwind.Invoke(null, new object[] { lat, lon, alt, ut });
			double spd = (double)miWspd.Invoke(null, new object[] { u, v, 0.0 });

			if (miWdirDegrees != null)
			{
				dirDeg = (double)miWdirDegrees.Invoke(null, new object[] { u, v });
			}
			if (miWdirCardinal != null)
			{
				cardinal = (string)miWdirCardinal.Invoke(null, new object[] { dirDeg });
			}

			return spd;
		}
	

}
}
