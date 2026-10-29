#region license
/* 
 * [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 *
 * SCAN_InstrumentView - Real-time scientific instrument visual display renderer
 * Generates dynamic 320x240 radargram, IR cloud imagery, gravity anomaly heatmaps,
 * ocean wave spectrum, greenhouse gas absorption, and space weather magnetosphere displays.
 */
#endregion

using System;
using System.Collections.Generic;
using UnityEngine;
using SCANsat.SCAN_PartModules;

namespace SCANsat.SCAN_UI.UI_Framework
{
	public enum InstrumentDisplayMode
	{
		Weather = 0,
		Subsurface = 1,
		Gravity = 2,
		Ocean = 3,
		Atmosphere = 4,
		SpaceWeather = 5
	}

	public class SCAN_InstrumentView
	{
		private const int WIDTH = 320;
		private const int HEIGHT = 240;

		private Texture2D texture;
		private Color32[] pixels;
		private float lastUpdate = 0f;
		private const float UPDATE_INTERVAL = 0.12f;

		private List<InstrumentDisplayMode> availableModes = new List<InstrumentDisplayMode>();
		private int currentModeIndex = 0;

		public bool IsActive { get; private set; }
		public int AvailableInstrumentCount { get { return availableModes.Count; } }

		private Vessel currentVessel;
		private SCANweather cachedWeather;
		private SCANsubsurface cachedSubsurface;
		private SCANgravity cachedGravity;
		private SCANocean cachedOcean;
		private SCANatmosphere cachedAtmosphere;
		private SCANspaceWeather cachedSpaceWeather;

		private string titleString = "";
		private string infoString = "";

		public SCAN_InstrumentView()
		{
			texture = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false);
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = FilterMode.Bilinear;
			pixels = new Color32[WIDTH * HEIGHT];
		}

		public void Free()
		{
			if (texture != null)
			{
				MonoBehaviour.Destroy(texture);
				texture = null;
			}
			pixels = null;
			IsActive = false;
		}

		public Texture GetTexture()
		{
			return texture;
		}

		public string GetNameString()
		{
			return titleString;
		}

		public string GetInfoString()
		{
			return infoString;
		}

		public void NextMode()
		{
			if (availableModes.Count <= 1) return;
			currentModeIndex = (currentModeIndex + 1) % availableModes.Count;
			RenderCurrentMode(true);
		}

		public bool Update(Vessel v)
		{
			if (v == null)
			{
				IsActive = false;
				return false;
			}

			currentVessel = v;
			cachedWeather = v.FindPartModuleImplementing<SCANweather>();
			cachedSubsurface = v.FindPartModuleImplementing<SCANsubsurface>();
			cachedGravity = v.FindPartModuleImplementing<SCANgravity>();
			cachedOcean = v.FindPartModuleImplementing<SCANocean>();
			cachedAtmosphere = v.FindPartModuleImplementing<SCANatmosphere>();
			cachedSpaceWeather = v.FindPartModuleImplementing<SCANspaceWeather>();

			availableModes.Clear();
			if (cachedWeather != null) availableModes.Add(InstrumentDisplayMode.Weather);
			if (cachedSubsurface != null) availableModes.Add(InstrumentDisplayMode.Subsurface);
			if (cachedGravity != null) availableModes.Add(InstrumentDisplayMode.Gravity);
			if (cachedOcean != null) availableModes.Add(InstrumentDisplayMode.Ocean);
			if (cachedAtmosphere != null) availableModes.Add(InstrumentDisplayMode.Atmosphere);
			if (cachedSpaceWeather != null) availableModes.Add(InstrumentDisplayMode.SpaceWeather);

			if (availableModes.Count == 0)
			{
				IsActive = false;
				return false;
			}

			if (currentModeIndex >= availableModes.Count)
			{
				currentModeIndex = 0;
			}

			IsActive = true;

			if (Time.realtimeSinceStartup - lastUpdate > UPDATE_INTERVAL)
			{
				lastUpdate = Time.realtimeSinceStartup;
				RenderCurrentMode(false);
			}

			return true;
		}

		private void RenderCurrentMode(bool force)
		{
			if (availableModes.Count == 0 || texture == null) return;

			InstrumentDisplayMode mode = availableModes[currentModeIndex];
			switch (mode)
			{
				case InstrumentDisplayMode.Weather:
					DrawWeather();
					break;
				case InstrumentDisplayMode.Subsurface:
					DrawSubsurface();
					break;
				case InstrumentDisplayMode.Gravity:
					DrawGravity();
					break;
				case InstrumentDisplayMode.Ocean:
					DrawOcean();
					break;
				case InstrumentDisplayMode.Atmosphere:
					DrawAtmosphere();
					break;
				case InstrumentDisplayMode.SpaceWeather:
					DrawSpaceWeather();
					break;
			}

			texture.SetPixels32(pixels);
			texture.Apply(false);
		}

		#region Drawing Primitives

		private void Clear(Color32 c)
		{
			for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
		}

		private void SetPixel(int x, int y, Color32 c)
		{
			if (x < 0 || x >= WIDTH || y < 0 || y >= HEIGHT) return;
			pixels[y * WIDTH + x] = c;
		}

		private void BlendPixel(int x, int y, Color32 c)
		{
			if (x < 0 || x >= WIDTH || y < 0 || y >= HEIGHT) return;
			int idx = y * WIDTH + x;
			Color32 bg = pixels[idx];
			float a = c.a / 255f;
			byte r = (byte)(bg.r * (1f - a) + c.r * a);
			byte g = (byte)(bg.g * (1f - a) + c.g * a);
			byte b = (byte)(bg.b * (1f - a) + c.b * a);
			pixels[idx] = new Color32(r, g, b, 255);
		}

		private void DrawLine(int x0, int y0, int x1, int y1, Color32 c)
		{
			int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
			int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
			int err = dx + dy, e2;
			while (true)
			{
				SetPixel(x0, y0, c);
				if (x0 == x1 && y0 == y1) break;
				e2 = 2 * err;
				if (e2 >= dy) { err += dy; x0 += sx; }
				if (e2 <= dx) { err += dx; y0 += sy; }
			}
		}

		private void DrawRect(int x, int y, int w, int h, Color32 c)
		{
			for (int i = x; i < x + w; i++) { SetPixel(i, y, c); SetPixel(i, y + h - 1, c); }
			for (int j = y; j < y + h; j++) { SetPixel(x, j, c); SetPixel(x + w - 1, j, c); }
		}

		private void FillRect(int x, int y, int w, int h, Color32 c)
		{
			int xEnd = Math.Min(WIDTH, x + w);
			int yEnd = Math.Min(HEIGHT, y + h);
			for (int j = Math.Max(0, y); j < yEnd; j++)
			{
				int row = j * WIDTH;
				for (int i = Math.Max(0, x); i < xEnd; i++)
				{
					pixels[row + i] = c;
				}
			}
		}

		private static Color32 LerpColor(Color32 a, Color32 b, float t)
		{
			t = Mathf.Clamp01(t);
			return new Color32(
				(byte)(a.r + (b.r - a.r) * t),
				(byte)(a.g + (b.g - a.g) * t),
				(byte)(a.b + (b.b - a.b) * t),
				(byte)(a.a + (b.a - a.a) * t)
			);
		}

		private static Color32 TurboLUT(float t)
		{
			t = Mathf.Clamp01(t);
			// Jet/Turbo rainbow: 0.0=Deep Blue -> 0.25=Cyan -> 0.5=Green -> 0.75=Yellow -> 1.0=Red
			if (t < 0.25f)
				return LerpColor(new Color32(10, 25, 120, 255), new Color32(20, 180, 220, 255), t / 0.25f);
			if (t < 0.5f)
				return LerpColor(new Color32(20, 180, 220, 255), new Color32(40, 220, 60, 255), (t - 0.25f) / 0.25f);
			if (t < 0.75f)
				return LerpColor(new Color32(40, 220, 60, 255), new Color32(255, 230, 20, 255), (t - 0.5f) / 0.25f);
			return LerpColor(new Color32(255, 230, 20, 255), new Color32(230, 30, 20, 255), (t - 0.75f) / 0.25f);
		}

		#endregion

		#region Mode 1: Weather (气象卫星红外云图)

		private void DrawWeather()
		{
			Clear(new Color32(6, 10, 18, 255));

			float ut = (float)Planetarium.GetUniversalTime();
			float lat = currentVessel != null ? (float)currentVessel.latitude : 0f;
			float lon = currentVessel != null ? (float)currentVessel.longitude : 0f;
			float cloudCover = cachedWeather != null ? (float)cachedWeather.currentCloudCover : 0.65f;
			float precipRate = cachedWeather != null ? (float)cachedWeather.currentPrecipRate : 0f;
			float windSpeed = cachedWeather != null ? (float)cachedWeather.currentWindSpeed : 8f;

			titleString = "气象卫星多通道红外亮温云图 (FY-4 / FY-3 IR Channel)";
			infoString = string.Format("通道: 10.8µm IR | 云量: {0:F0}% | 降水: {1:F1} mm/h | 视场亮温: {2:F1} K", cloudCover * 100f, precipRate, 280f - cloudCover * 75f);

			int cx = 160;
			int cy = 120;
			int r = 105;

			// Draw grid rings
			for (int d = 35; d <= r; d += 35)
			{
				DrawCircle(cx, cy, d, new Color32(18, 40, 60, 255));
			}

			// Render planet disc and dynamic weather cloud spirals
			for (int y = cy - r; y <= cy + r; y++)
			{
				if (y < 0 || y >= HEIGHT) continue;
				int dy = y - cy;
				int row = y * WIDTH;
				for (int x = cx - r; x <= cx + r; x++)
				{
					if (x < 0 || x >= WIDTH) continue;
					int dx = x - cx;
					float distSq = dx * dx + dy * dy;
					if (distSq > r * r) continue;

					float dist = Mathf.Sqrt(distSq);
					float angle = Mathf.Atan2(dy, dx);

					// Dynamic atmospheric vortex swirl
					float swirl = angle + (dist / r) * 3.2f - ut * 0.08f;
					float n1 = Mathf.Sin(dx * 0.05f + swirl * 2f) * Mathf.Cos(dy * 0.05f - swirl);
					float n2 = Mathf.Sin((dx + dy) * 0.08f + ut * 0.15f) * 0.5f;
					float cloudVal = Mathf.Clamp01((n1 + n2 + 0.5f) * cloudCover * 1.3f);

					// Base planet earth surface
					Color32 baseColor = ((dx + dy) % 32 < 16) ? new Color32(14, 28, 55, 255) : new Color32(20, 36, 68, 255);

					Color32 pixelColor;
					if (cloudVal > 0.45f)
					{
						// IR Brightness temperature mapping:
						// Cold high clouds = bright white/red storm core; warm low clouds = grey/cyan
						float stormCore = Mathf.Clamp01((cloudVal - 0.72f) * 3.5f + precipRate * 0.2f);
						if (stormCore > 0.6f)
						{
							// Severe convective storm core (< 215 K)
							pixelColor = LerpColor(new Color32(255, 40, 50, 255), new Color32(255, 240, 255, 255), stormCore);
						}
						else if (cloudVal > 0.68f)
						{
							// Dense high cloud anvil (220 - 240 K)
							pixelColor = new Color32(245, 248, 255, 255);
						}
						else
						{
							// Mid-to-low level stratiform clouds (250 - 275 K)
							pixelColor = LerpColor(new Color32(120, 160, 195, 255), new Color32(210, 225, 240, 255), (cloudVal - 0.45f) / 0.23f);
						}
					}
					else
					{
						pixelColor = baseColor;
					}

					pixels[row + x] = pixelColor;
				}
			}

			// Sub-satellite crosshair
			DrawLine(cx - 15, cy, cx + 15, cy, new Color32(0, 255, 170, 255));
			DrawLine(cx, cy - 15, cx, cy + 15, new Color32(0, 255, 170, 255));

			// Animated scanning line sweep
			int scanY = (int)((ut * 25f) % (HEIGHT - 20)) + 10;
			for (int x = 20; x < WIDTH - 20; x++)
			{
				BlendPixel(x, scanY, new Color32(0, 255, 200, 100));
			}

			// Bottom IR Palette scale bar (200K -> 300K)
			for (int bx = 20; bx < WIDTH - 20; bx++)
			{
				float t = (bx - 20f) / (WIDTH - 40f);
				Color32 barCol = TurboLUT(1f - t);
				for (int by = 6; by <= 12; by++) SetPixel(bx, by, barCol);
			}
		}

		private void DrawCircle(int cx, int cy, int r, Color32 c)
		{
			int x = r, y = 0;
			int err = 0;
			while (x >= y)
			{
				SetPixel(cx + x, cy + y, c);
				SetPixel(cx + y, cy + x, c);
				SetPixel(cx - y, cy + x, c);
				SetPixel(cx - x, cy + y, c);
				SetPixel(cx - x, cy - y, c);
				SetPixel(cx - y, cy - x, c);
				SetPixel(cx + y, cy - x, c);
				SetPixel(cx + x, cy - y, c);
				y++;
				err += 1 + 2 * y;
				if (2 * (err - x) + 1 > 0)
				{
					x--;
					err += 1 - 2 * x;
				}
			}
		}

		#endregion

		#region Mode 2: Subsurface GPR (次表层探地雷达回波图)

		private void DrawSubsurface()
		{
			Clear(new Color32(8, 14, 12, 255));

			float ut = (float)Planetarium.GetUniversalTime();
			float regolith = cachedSubsurface != null ? (float)cachedSubsurface.regolithThickness : 12.5f;
			float bedrock = cachedSubsurface != null ? (float)cachedSubsurface.bedrockDepth : 650f;
			bool hasIce = cachedSubsurface != null && cachedSubsurface.detectedFeature != null && cachedSubsurface.detectedFeature.Contains("水冰");
			bool hasTube = cachedSubsurface != null && cachedSubsurface.detectedFeature != null && (cachedSubsurface.detectedFeature.Contains("熔岩") || cachedSubsurface.detectedFeature.Contains("空腔"));

			titleString = "次表层深部穿透探地雷达剖面 (RoPeR / MARSIS GPR Radargram)";
			infoString = string.Format("深度范围: 0-5000m | 风化层: {0:F1}m | 基岩埋深: {1:F0}m | 结构: {2}", regolith, bedrock, hasIce ? "富水冰高介电层" : (hasTube ? "地下熔岩管道空洞" : "稳定沉积层理"));

			// Background radar grid lines (depth and track)
			for (int y = 20; y < HEIGHT - 10; y += 35)
			{
				for (int x = 30; x < WIDTH - 10; x += 4) SetPixel(x, y, new Color32(18, 38, 26, 255));
			}
			for (int x = 30; x < WIDTH - 10; x += 45)
			{
				for (int y = 20; y < HEIGHT - 10; y += 4) SetPixel(x, y, new Color32(18, 38, 26, 255));
			}

			// Surface ground direct return (y = 210)
			for (int x = 30; x < WIDTH - 40; x++)
			{
				float surfNoise = Mathf.Sin(x * 0.15f + ut * 0.5f) * 1.5f;
				int sy = 210 + (int)surfNoise;
				SetPixel(x, sy, new Color32(180, 255, 160, 255));
				SetPixel(x, sy - 1, new Color32(100, 220, 90, 255));
			}

			// Regolith particulate volume scattering
			for (int x = 30; x < WIDTH - 40; x++)
			{
				for (int y = 185; y < 208; y++)
				{
					if (((x * 17 + y * 31 + (int)(ut * 10)) % 19) == 0)
					{
						SetPixel(x, y, new Color32(60, 140, 80, 255));
					}
				}
			}

			// Bedrock interface reflector line (y ~ 145)
			for (int x = 30; x < WIDTH - 40; x++)
			{
				float bedNoise = Mathf.Sin(x * 0.04f) * 6f + Mathf.Cos(x * 0.09f) * 3f;
				int by = 150 + (int)bedNoise;
				SetPixel(x, by, new Color32(230, 245, 120, 255));
				SetPixel(x, by - 1, new Color32(160, 180, 70, 255));
			}

			// Water ice layer interface (bright cyan reflector band)
			if (hasIce)
			{
				int iceY = 175;
				for (int x = 30; x < WIDTH - 40; x++)
				{
					float inoise = Mathf.Sin(x * 0.08f + ut * 0.2f) * 2f;
					int iy = iceY + (int)inoise;
					SetPixel(x, iy, new Color32(80, 240, 255, 255));
					SetPixel(x, iy - 1, new Color32(40, 180, 220, 255));
				}
			}

			// Lava tube hyperbolic diffraction arcs (parabolas)
			if (hasTube || ((int)ut % 8 < 5))
			{
				int hx = 160;
				int hy = 110;
				for (int dx = -35; dx <= 35; dx++)
				{
					int dy = (int)(dx * dx * 0.035f);
					int py = hy - dy;
					if (py >= 20 && py < HEIGHT - 10)
					{
						SetPixel(hx + dx, py, new Color32(255, 215, 40, 255));
						SetPixel(hx + dx, py - 1, new Color32(180, 140, 20, 255));
					}
				}
			}

			// A-scan live oscilloscope trace on right border (WIDTH - 35 to WIDTH - 10)
			for (int y = 20; y < 220; y++)
			{
				float amp = Mathf.Sin(y * 0.2f + ut * 4f) * 3f;
				if (y > 205 && y < 215) amp += 12f; // surface peak
				if (y > 145 && y < 155) amp += 8f;  // bedrock peak
				int ox = (WIDTH - 22) + (int)amp;
				SetPixel(ox, y, new Color32(255, 255, 255, 255));
			}

			// Depth scale vertical axis line
			DrawLine(30, 20, 30, 220, new Color32(120, 200, 150, 255));
			for (int tick = 20; tick <= 220; tick += 40)
			{
				DrawLine(26, tick, 30, tick, new Color32(120, 200, 150, 255));
			}
		}

		#endregion

		#region Mode 3: Gravimetry (全球重力场异常球谐反演热力图)

		private void DrawGravity()
		{
			Clear(new Color32(6, 8, 16, 255));

			float ut = (float)Planetarium.GetUniversalTime();
			float fa = cachedGravity != null ? (float)cachedGravity.currentFreeAirMGal : 24.5f;
			float bg = cachedGravity != null ? (float)cachedGravity.currentBouguerMGal : -15.2f;
			bool hasMascon = cachedGravity != null && !string.IsNullOrEmpty(cachedGravity.currentMasconName) && cachedGravity.currentMasconName != "无显著异常" && cachedGravity.currentMasconName != "None";

			titleString = "全球重力场异常球谐反演热力图 (GRACE-FO / GOCE Gravimetry)";
			infoString = string.Format("自由空气重力异常: {0:+0.0;-0.0;0.0} mGal | 布格异常: {1:+0.0;-0.0;0.0} mGal | 状态: {2}", fa, bg, hasMascon ? "检测到高密度 Mascon 质量密集区" : "正常球谐重力水准");

			int cx = 160;
			int cy = 120;
			int r = 100;

			// Draw spherical harmonic gravity field contour disc
			for (int y = cy - r; y <= cy + r; y++)
			{
				if (y < 0 || y >= HEIGHT) continue;
				int dy = y - cy;
				int row = y * WIDTH;
				for (int x = cx - r; x <= cx + r; x++)
				{
					if (x < 0 || x >= WIDTH) continue;
					int dx = x - cx;
					float distSq = dx * dx + dy * dy;
					if (distSq > r * r) continue;

					float dist = Mathf.Sqrt(distSq);
					float angle = Mathf.Atan2(dy, dx);

					// Spherical harmonic potential simulation
					float gVal = Mathf.Sin(dx * 0.045f + ut * 0.05f) * Mathf.Cos(dy * 0.045f)
					           + Mathf.Sin(angle * 3f) * 0.35f
					           + Mathf.Cos(dist * 0.08f) * 0.25f;

					// If Mascon present, add a strong positive gravity anomaly peak
					if (hasMascon)
					{
						float masconDist = Mathf.Sqrt((dx - 20) * (dx - 20) + (dy + 15) * (dy + 15));
						if (masconDist < 45f)
						{
							gVal += (1f - masconDist / 45f) * 1.8f;
						}
					}

					float normG = Mathf.Clamp01((gVal + 1.2f) / 2.4f);
					Color32 col = TurboLUT(normG);

					// Iso-gravity contour lines
					if (Mathf.Abs(normG * 10f - Mathf.Round(normG * 10f)) < 0.04f)
					{
						col = new Color32(20, 20, 20, 255);
					}

					pixels[row + x] = col;
				}
			}

			// Mascon target ring overlay
			if (hasMascon)
			{
				DrawCircle(cx + 20, cy - 15, 18, new Color32(255, 230, 40, 255));
				DrawCircle(cx + 20, cy - 15, 30, new Color32(255, 60, 40, 200));
			}

			// Center crosshair
			DrawLine(cx - 12, cy, cx + 12, cy, new Color32(255, 255, 255, 255));
			DrawLine(cx, cy - 12, cx, cy + 12, new Color32(255, 255, 255, 255));

			// Bottom mGal color scale bar (-120 ~ +120 mGal)
			for (int bx = 30; bx < WIDTH - 30; bx++)
			{
				float t = (bx - 30f) / (WIDTH - 60f);
				Color32 c = TurboLUT(t);
				for (int by = 6; by <= 12; by++) SetPixel(bx, by, c);
			}
		}

		#endregion

		#region Mode 4: Oceanography (海洋动力波高浪谱与水色图)

		private void DrawOcean()
		{
			Clear(new Color32(4, 12, 24, 255));

			float ut = (float)Planetarium.GetUniversalTime();
			float swh = cachedOcean != null ? (float)cachedOcean.swh : 2.8f;
			float chl = cachedOcean != null ? (float)cachedOcean.chlorophyll : 1.45f;
			string state = cachedOcean != null ? cachedOcean.seaStateDesc : "中浪 (Moderate)";

			titleString = "海洋微波有效波高 (SWH) 与水色叶绿素水华监测图 (HY-2 / HY-1)";
			infoString = string.Format("有效波高: {0:F2}m ({1}) | 叶绿素-a: {2:F2} mg/m³ | 盐度: {3:F1} PSU", swh, state, chl, 34.8f);

			// Ocean gyre and chlorophyll concentration background
			for (int y = 0; y < HEIGHT; y++)
			{
				int row = y * WIDTH;
				for (int x = 0; x < WIDTH; x++)
				{
					float waveNoise = Mathf.Sin(x * 0.06f + ut * 1.5f) * Mathf.Cos(y * 0.06f - ut * 0.8f);
					float chlPlume = Mathf.Sin((x + y) * 0.03f + ut * 0.1f) * Mathf.Cos(x * 0.04f);

					Color32 oCol;
					if (chlPlume > 0.35f)
					{
						// High chlorophyll algal bloom zone (fluorescent green / amber)
						oCol = LerpColor(new Color32(30, 160, 90, 255), new Color32(230, 220, 50, 255), (chlPlume - 0.35f) * 2f);
					}
					else
					{
						// Deep blue oligotrophic open ocean with wave modulation
						oCol = LerpColor(new Color32(8, 22, 60, 255), new Color32(18, 65, 140, 255), waveNoise * 0.5f + 0.5f);
					}
					pixels[row + x] = oCol;
				}
			}

			// Left: 2D Directional Ocean Wave Energy Spectrum polar display
			int scx = 80;
			int scy = 120;
			DrawCircle(scx, scy, 55, new Color32(0, 220, 255, 120));
			DrawCircle(scx, scy, 35, new Color32(0, 220, 255, 100));
			DrawCircle(scx, scy, 18, new Color32(0, 220, 255, 80));
			DrawLine(scx - 60, scy, scx + 60, scy, new Color32(0, 200, 255, 90));
			DrawLine(scx, scy - 60, scx, scy + 60, new Color32(0, 200, 255, 90));

			// Wave energy ellipse (elongated along dominant swell direction)
			float swellDir = ut * 0.3f;
			for (float a = 0; a < 6.28f; a += 0.05f)
			{
				float rDir = (30f + swh * 6f) * (1f + 0.4f * Mathf.Cos(2f * (a - swellDir)));
				int px = scx + (int)(Mathf.Cos(a) * rDir);
				int py = scy + (int)(Mathf.Sin(a) * rDir);
				SetPixel(px, py, new Color32(255, 230, 40, 255));
			}

			// Right: SWH wave height profile graph
			DrawRect(165, 30, 140, 180, new Color32(0, 180, 240, 200));
			for (int gx = 166; gx < 304; gx++)
			{
				float waveProfile = Mathf.Sin((gx - 165) * 0.18f + ut * 3f) * (swh * 6f)
				                  + Mathf.Sin((gx - 165) * 0.35f - ut * 2f) * (swh * 2.5f);
				int gy = 120 + (int)waveProfile;
				if (gy >= 32 && gy < 208) SetPixel(gx, gy, new Color32(255, 255, 255, 255));
			}
		}

		#endregion

		#region Mode 5: Greenhouse Gas / Carbon (温室气体吸收光谱与浓度图)

		private void DrawAtmosphere()
		{
			Clear(new Color32(10, 14, 20, 255));

			float ut = (float)Planetarium.GetUniversalTime();
			float xco2 = cachedAtmosphere != null ? (float)cachedAtmosphere.xco2 : 418.5f;
			float xch4 = cachedAtmosphere != null ? (float)cachedAtmosphere.xch4 : 1890f;
			float aod = cachedAtmosphere != null ? (float)cachedAtmosphere.aod : 0.125f;

			titleString = "大气二氧化碳/甲烷柱浓度光谱反演图 (TanSat / OCO-2)";
			infoString = string.Format("XCO₂: {0:F1} ppm | XCH₄: {1:F0} ppb | 气溶胶AOD: {2:F3} | 动态: 区域碳排放羽流", xco2, xch4, aod);

			// Top Half: High-resolution CO2 Molecular Absorption Spectrum (1.61µm / 2.06µm)
			FillRect(15, 125, 290, 95, new Color32(6, 10, 16, 255));
			DrawRect(15, 125, 290, 95, new Color32(40, 90, 120, 255));

			// Grid lines
			for (int y = 145; y <= 205; y += 25)
			{
				for (int x = 16; x < 304; x += 4) SetPixel(x, y, new Color32(20, 45, 65, 255));
			}
			for (int x = 45; x <= 285; x += 40)
			{
				for (int y = 126; y < 219; y += 4) SetPixel(x, y, new Color32(20, 45, 65, 255));
			}

			// Draw Fraunhofer CO2 absorption doublet dips (P-branch & R-branch)
			for (int x = 16; x < 304; x++)
			{
				float normX = (x - 160) / 70f;
				// Double absorption wells around central resonant band
				float absorption = Mathf.Exp(-normX * normX * 0.8f) * (0.6f + 0.35f * Mathf.Cos(normX * 14f));
				float continuum = 205f;
				int sy = (int)(continuum - absorption * 65f * (xco2 / 400f));
				SetPixel(x, sy, new Color32(60, 230, 255, 255));
				SetPixel(x, sy - 1, new Color32(30, 150, 180, 255));
			}

			// Bottom Half: False-color surface XCO2 plume footprint map (390 - 430 ppm)
			FillRect(15, 20, 290, 95, new Color32(12, 18, 28, 255));
			DrawRect(15, 20, 290, 95, new Color32(40, 90, 120, 255));

			for (int y = 21; y < 114; y++)
			{
				int row = y * WIDTH;
				for (int x = 16; x < 304; x++)
				{
					float plumeDist = Mathf.Sqrt((x - 180) * (x - 180) + (y - 65) * (y - 65));
					float plume = Mathf.Exp(-plumeDist * 0.05f) * Mathf.Sin((x + y) * 0.08f + ut * 0.3f);
					float cVal = Mathf.Clamp01((xco2 - 400f) / 30f + plume * 0.4f);

					// Cold cyan/teal (ecological sink) -> bright amber/crimson (industrial emission plume)
					Color32 cColor = LerpColor(new Color32(20, 140, 160, 255), new Color32(255, 60, 40, 255), cVal);
					pixels[row + x] = cColor;
				}
			}

			// Central emission hotspot marker
			DrawCircle(180, 65, 8, new Color32(255, 255, 255, 255));
		}

		#endregion

		#region Mode 6: Space Weather (空间天气地磁偶极子与辐射带切片图)

		private void DrawSpaceWeather()
		{
			Clear(new Color32(6, 6, 12, 255));

			float ut = (float)Planetarium.GetUniversalTime();
			float bTotal = cachedSpaceWeather != null ? (float)cachedSpaceWeather.magneticField_nT : 48500f;
			float tec = cachedSpaceWeather != null ? (float)cachedSpaceWeather.tecUnits : 24.5f;
			float proton = cachedSpaceWeather != null ? (float)cachedSpaceWeather.protonFlux : 1.2e4f;
			string storm = cachedSpaceWeather != null ? cachedSpaceWeather.stormLevelDesc : "地磁活动平静 (Quiet)";

			titleString = "空间地磁偶极子与范艾伦辐射带切片图 (CSES / Van Allen Probes)";
			infoString = string.Format("地磁总强 |B|: {0:F0} nT | TEC: {1:F1} TECU | 质子通量: {2:E1} p/cm²/s | 状态: {3}", bTotal, tec, proton, storm);

			int pcx = 110;
			int pcy = 120;
			int pr = 22; // Planet radius in pixels

			// Solar wind bow shock curve on sunward side (right)
			for (int y = 15; y < HEIGHT - 15; y++)
			{
				int dy = y - pcy;
				int sx = 230 - (int)(dy * dy * 0.008f);
				if (sx >= 0 && sx < WIDTH)
				{
					BlendPixel(sx, y, new Color32(255, 140, 20, 180));
					BlendPixel(sx - 1, y, new Color32(255, 80, 10, 100));
				}
			}

			// Van Allen Radiation Belts (Inner Proton Belt and Outer Electron Belt)
			// Outer Belt (toroid ~ 3.5 to 5.5 Rp)
			for (int dx = -100; dx <= 100; dx++)
			{
				for (int dy = -60; dy <= 60; dy++)
				{
					float d = Mathf.Sqrt(dx * dx + dy * dy * 2.2f);
					if (d >= 50f && d <= 90f)
					{
						float t = 1f - Mathf.Abs(d - 70f) / 20f;
						BlendPixel(pcx + dx, pcy + dy, new Color32(240, 50, 210, (byte)(t * 90)));
					}
				}
			}

			// Inner Belt (toroid ~ 1.5 to 2.5 Rp)
			for (int dx = -50; dx <= 50; dx++)
			{
				for (int dy = -30; dy <= 30; dy++)
				{
					float d = Mathf.Sqrt(dx * dx + dy * dy * 2.2f);
					if (d >= 26f && d <= 46f)
					{
						float t = 1f - Mathf.Abs(d - 36f) / 10f;
						BlendPixel(pcx + dx, pcy + dy, new Color32(255, 200, 30, (byte)(t * 130)));
					}
				}
			}

			// Dipole Magnetic Field Lines: r = R0 * sin^2(theta)
			for (int L = 35; L <= 110; L += 15)
			{
				for (float th = 0.1f; th < 3.05f; th += 0.04f)
				{
					float rDip = L * Mathf.Sin(th) * Mathf.Sin(th);
					int fx = pcx + (int)(rDip * Mathf.Cos(th));
					int fy = pcy + (int)(rDip * Mathf.Sin(th));
					BlendPixel(fx, fy, new Color32(50, 200, 255, 120));

					// Symmetric Southern loop
					int fySouth = pcy - (int)(rDip * Mathf.Sin(th));
					BlendPixel(fx, fySouth, new Color32(50, 200, 255, 120));
				}
			}

			// Central planet body
			FillRect(pcx - pr, pcy - pr, pr * 2, pr * 2, new Color32(0, 0, 0, 0));
			for (int y = pcy - pr; y <= pcy + pr; y++)
			{
				int dy = y - pcy;
				int row = y * WIDTH;
				for (int x = pcx - pr; x <= pcx + pr; x++)
				{
					int dx = x - pcx;
					if (dx * dx + dy * dy <= pr * pr)
					{
						// Half sunlit (right side), half night (left side)
						Color32 planetCol = dx > 0 ? new Color32(40, 110, 190, 255) : new Color32(10, 25, 50, 255);
						pixels[row + x] = planetCol;
					}
				}
			}
			DrawCircle(pcx, pcy, pr, new Color32(80, 180, 255, 255));

			// Spacecraft current orbit position marker (Blinking gold star)
			float scAngle = ut * 0.4f;
			float scR = 48f; // orbital radius
			int scx = pcx + (int)(Mathf.Cos(scAngle) * scR);
			int scy = pcy + (int)(Mathf.Sin(scAngle) * scR * 0.7f);
			DrawCircle(scx, scy, 3, new Color32(255, 255, 0, 255));
			DrawLine(scx - 5, scy, scx + 5, scy, new Color32(255, 255, 255, 255));
			DrawLine(scx, scy - 5, scx, scy + 5, new Color32(255, 255, 255, 255));

			// Right-side HUD: Geomagnetic Kp storm bar gauge (0 to 9)
			DrawRect(260, 25, 45, 190, new Color32(40, 80, 120, 255));
			int kpLevel = (int)Mathf.Clamp((bTotal - 30000f) / 5000f, 1f, 9f);
			for (int k = 1; k <= 9; k++)
			{
				int by = 30 + (k - 1) * 20;
				Color32 barCol = k <= 3 ? new Color32(40, 220, 60, 255) : (k <= 6 ? new Color32(255, 210, 30, 255) : new Color32(255, 40, 30, 255));
				if (k <= kpLevel) FillRect(265, by, 35, 16, barCol);
				else DrawRect(265, by, 35, 16, new Color32(25, 40, 55, 255));
			}
		}

		#endregion
	}
}
