#region license
/*
 * [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 * 
 * SCANgravity - Satellite gravimetry & geodetic anomaly sounding (GRACE / GOCE)
 */
#endregion

using System;
using System.Text;
using UnityEngine;

namespace SCANsat.SCAN_PartModules
{
	public class SCANgravity : PartModule
	{
		[KSPField(isPersistant = true, guiActive = true, guiName = "重力梯度系统")]
		public string gravityStatus = "高精干涉中";

		[KSPField(isPersistant = false, guiActive = true, guiName = "空间重力加速度", groupName = "Gravimetry", groupDisplayName = "🌐 卫星重力与大地水准面")]
		public string gFieldStr = "测定中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "自由空间异常(mGal)", groupName = "Gravimetry", groupDisplayName = "🌐 卫星重力与大地水准面")]
		public string freeAirAnomalyStr = "测定中...";

		[KSPField(isPersistant = false, guiActive = true, guiName = "地壳质量分布", groupName = "Gravimetry", groupDisplayName = "🌐 卫星重力与大地水准面")]
		public string masconStr = "分析中...";

		[KSPField]
		public float updateInterval = 1.5f;

		private float lastUpdate = 0f;

		public double currentG;
		public double currentFreeAirMGal;
		public double currentBouguerMGal;
		public string currentMasconName = "地壳均衡常态区";

		public void Update()
		{
			if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
			if (Time.time - lastUpdate < updateInterval) return;
			lastUpdate = Time.time;

			calculateGravityAnomaly();
		}

		private void calculateGravityAnomaly()
		{
			CelestialBody body = vessel.mainBody;
			if (body == null) return;

			double r = (vessel.CoM - body.position).magnitude;
			if (r <= 0) return;

			// Actual point-mass gravity at orbit: g = GM / r^2
			double gTheoretical = (body.gravParameter) / (r * r);

			// Altitude above sea level
			double alt = vessel.altitude;
			double R0 = body.Radius;

			// Theoretical surface gravity at this latitude (Somigliana / normal gravity formula)
			double latRad = vessel.latitude * Math.PI / 180.0;
			double sinLat = Math.Sin(latRad);
			// J2 oblateness effect: delta g ~ 1.5 * J2 * (R/r)^2 * (3 sin^2(lat) - 1)
			double j2 = 0.00108263; // standard planetary flattening J2
			double oblatenessCorr = 1.0 + 1.5 * j2 * Math.Pow(R0 / r, 2.0) * (3.0 * sinLat * sinLat - 1.0);
			double gNormal = gTheoretical * oblatenessCorr;

			// Local topographical & mass variation (Spherical harmonic simulation)
			double lonRad = vessel.longitude * Math.PI / 180.0;
			double harmonic = Math.Sin(3.0 * latRad) * Math.Cos(2.0 * lonRad) * 0.000045
							+ Math.Cos(5.0 * latRad) * Math.Sin(4.0 * lonRad) * 0.000030
							+ Math.Sin(7.0 * lonRad) * 0.000020;

			// Terrain elevation contribution
			double terrainAlt = SCANUtil.getElevation(body, vessel.longitude, vessel.latitude);
			double terrainBouguer = (2.0 * Math.PI * 6.67430e-11 * 2670.0 * terrainAlt); // standard crustal density 2670 kg/m3

			currentG = gNormal * (1.0 + harmonic);
			// 1 m/s^2 = 100,000 mGal
			currentFreeAirMGal = (currentG - gNormal) * 100000.0;
			currentBouguerMGal = currentFreeAirMGal - (terrainBouguer * 100000.0);

			// Check for Masscons (Mass Concentrations)
			if (currentFreeAirMGal > 120.0)
			{
				currentMasconName = "⚠️ 强正引力异常区 (深部高密质量密集区 Mascon)";
			}
			else if (currentFreeAirMGal > 40.0)
			{
				currentMasconName = "正引力异常区 (致密玄武岩基底/高密矿床)";
			}
			else if (currentFreeAirMGal < -80.0)
			{
				currentMasconName = "⚠️ 强负引力异常区 (深部低密断裂带/深渊海沟)";
			}
			else if (currentFreeAirMGal < -30.0)
			{
				currentMasconName = "负引力异常区 (沉积盆地/巨型冰川覆盖层)";
			}
			else
			{
				currentMasconName = "地壳均衡常态区 (Isostatic Equilibrium)";
			}

			gFieldStr = string.Format("{0:F4} m/s² ({1:F2} g)", currentG, currentG / 9.80665);
			freeAirAnomalyStr = string.Format("{0:+0.0;-0.0;0.0} mGal", currentFreeAirMGal);
			masconStr = currentMasconName;
		}

		[KSPEvent(guiActive = true, guiName = "🌐 生成空间重力场与大地水准面简报", groupName = "Gravimetry")]
		public void ShowGravityBriefing()
		{
			calculateGravityAnomaly();
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine(string.Format("🛰️ 仪器型号: {0} 卫星高精重力梯度仪", part.partInfo.title));
			sb.AppendLine(string.Format("🪐 探测天体: {0} | 轨道高度: {1:N1} km", vessel.mainBody.bodyName, vessel.altitude / 1000.0));
			sb.AppendLine(string.Format("📍 测线位置: {0:F3}°{1}, {2:F3}°{3}",
				Math.Abs(vessel.latitude), vessel.latitude >= 0 ? "N" : "S",
				Math.Abs(vessel.longitude), vessel.longitude >= 0 ? "E" : "W"));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			sb.AppendLine("【轨道引力场精细测量参数】");
			sb.AppendLine(string.Format(" • 空间实测重力加速度:         {0:F6} m/s²", currentG));
			sb.AppendLine(string.Format(" • 理论正常参考重力场:         {0:F6} m/s²", (vessel.mainBody.gravParameter) / Math.Pow(vessel.altitude + vessel.mainBody.Radius, 2.0)));
			sb.AppendLine(string.Format(" • 自由空间重力异常 (Free-Air): {0:+0.00;-0.00;0.00} mGal", currentFreeAirMGal));
			sb.AppendLine(string.Format(" • 布格重力异常 (Bouguer Anom): {0:+0.00;-0.00;0.00} mGal", currentBouguerMGal));
			sb.AppendLine("【地下构造与质量密集区解算】");
			sb.AppendLine(string.Format(" • 地壳构造判识:               {0}", currentMasconName));
			sb.AppendLine(string.Format(" • 大地水准面高起伏 (Geoid):    {0:+0.0;-0.0;0.0} 米", currentFreeAirMGal * 0.15));
			sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

			PopupDialog.SpawnPopupDialog(
				new Vector2(0.5f, 0.5f),
				new Vector2(0.5f, 0.5f),
				new MultiOptionDialog(
					"SCANsatGravityBriefing",
					sb.ToString(),
					"🌐 空间大地测量与重力场球谐反演简报",
					HighLogic.UISkin,
					new DialogGUIButton("确认", null, true)
				),
				false,
				HighLogic.UISkin
			);
		}
	}
}
