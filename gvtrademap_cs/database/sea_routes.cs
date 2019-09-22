﻿/*-------------------------------------------------------------------------

 항로도

---------------------------------------------------------------------------*/

/*-------------------------------------------------------------------------
 define
---------------------------------------------------------------------------*/
//#define	DRAW_SEA_ROUTES_BOUNDINGBOX			// 항로도の바운딩 박스그리기
//#define	DRAW_POPUPS_BOUNDINGBOX				// ポップアップの바운딩 박스그리기
//#define	DISABLE_SEA_ROUTES_CHAIN_POINTS		// 항로도を出来るだけ繋げる処理무효

/*-------------------------------------------------------------------------
 using
---------------------------------------------------------------------------*/
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

using Utility;
using directx;
using System.Collections;

/*-------------------------------------------------------------------------

---------------------------------------------------------------------------*/
namespace gvtrademap_cs {
	/*-------------------------------------------------------------------------

	---------------------------------------------------------------------------*/
	public class SeaRoutes {
		// 선とする距離の최대
		private const float LINE_ROUTE_MAX = 15 * 15;
		// 선として추가する최저距離
		private const float ADD_POINT_MIN = 7 * 7;
		// 離れすぎた場合に추가する각도(degree)
		private const float ADD_POINT_ANGLE_GAP_MAX = 16;
		//		private const float			ADD_POINT_ANGLE_GAP_MAX			= 25;
		private const float ADD_POINT_ANGLE_LENGTH_SQ_MAX = 2000 * 2000;

		// 스크린샷の좌우の余裕
		private const int SCREEN_SHOT_BOUNDING_BOX_GAP_X = 64;
		// 스크린샷の상下の余裕
		private const int SCREEN_SHOT_BOUNDING_BOX_GAP_Y = 64;

		// 최신の항로以외の반투명具合
		private const float SEAROUTES_ALPHA = 0.4f;

		// 1つの선とする바운딩 박스の사이즈
		// 사이즈が대きすぎると그리기で損するため, 적당な대きさになったら선を분할する
		// 単位は지도좌표계
		private const float BB_SIZE_MAX = 250;
		private const float BB_POPUP_SIZE_MAX = 350f;
		// 화면외判定時の余白
		private const float BB_OUTSIDESCREEEN_OFFSET = 32f;

		// 스크린샷の분布체크単位(dot)
		private const int SS_DISTRIBUTION_X = 64;

		/*-------------------------------------------------------------------------
		 항로좌표
		---------------------------------------------------------------------------*/
		public class SeaRoutePoint {
			private Vector2 m_pos;			  // 좌표
			private int m_color_index;	  // 색번호
			private int m_color;			// 그리기색
											// 반투명値が0なので注意

			/*-------------------------------------------------------------------------

			---------------------------------------------------------------------------*/
			public Vector2 Position {
				get { return m_pos; }
				set { m_pos = value; }
			}
			public int ColorIndex { get { return m_color_index; } }
			public int Color { get { return m_color; } }

			/*-------------------------------------------------------------------------

			---------------------------------------------------------------------------*/
			public SeaRoutePoint(float x, float y, int color_index) {
				m_pos.X = x;
				m_pos.Y = y;
				m_color_index = color_index;
				m_color = DrawColor.GetColor(color_index);
			}
			public SeaRoutePoint(float x, float y) {
				m_pos.X = x;
				m_pos.Y = y;
				m_color_index = 0;
				m_color = DrawColor.GetColor(ColorIndex);
			}
			public SeaRoutePoint(Vector2 pos, int color_index) {
				m_pos = pos;
				m_color_index = color_index;
				m_color = DrawColor.GetColor(color_index);
			}
			public SeaRoutePoint(Vector2 pos) {
				m_pos = pos;
				m_color_index = 0;
				m_color = DrawColor.GetColor(ColorIndex);
			}
			public SeaRoutePoint(SeaRoutePoint p) {
				m_pos = p.Position;
				m_color_index = p.ColorIndex;
				m_color = p.Color;
			}
			/*-------------------------------------------------------------------------
			 距離の2乗を返す
			---------------------------------------------------------------------------*/
			public float LengthSq(SeaRoutePoint p) {
				Vector2 l = new Vector2(p.Position.X - m_pos.X,
											p.Position.Y - m_pos.Y);
				return l.LengthSq();
			}
			/*-------------------------------------------------------------------------
			 距離を返す
			---------------------------------------------------------------------------*/
			public float Length(SeaRoutePoint p) {
				Vector2 l = new Vector2(p.Position.X - m_pos.X,
											p.Position.Y - m_pos.Y);
				return l.Length();
			}

			/*-------------------------------------------------------------------------
			 距離の2乗を返す
			 ループを考慮し, 近いほうの距離を返す
			 近い위치を near_p に返す
			 near_p == p ならループを考慮しない위치が最短
			---------------------------------------------------------------------------*/
			public float LengthSq(SeaRoutePoint p, int size_x) {
				SeaRoutePoint near = null;
				return LengthSq(p, size_x, ref near);
			}
			public float LengthSq(SeaRoutePoint p, int size_x, ref SeaRoutePoint near_p) {
				float l1 = LengthSq(p);
				SeaRoutePoint p2 = new SeaRoutePoint(p);
				p2.build_loop_x(size_x);
				SeaRoutePoint p3 = new SeaRoutePoint(p);
				p3.build_loop_x(-size_x);
				float l2 = LengthSq(p2);
				float l3 = LengthSq(p3);

				if (l1 < l2) {
					if (l1 < l3) {
						near_p = p;
						return l1;
					} else {
						near_p = p3;
						return l3;
					}
				} else {
					if (l2 < l3) {
						near_p = p2;
						return l2;
					} else {
						near_p = p3;
						return l3;
					}
				}
			}

			/*-------------------------------------------------------------------------
			 距離を返す
			 ループを考慮し, 近いほうの距離を返す
			---------------------------------------------------------------------------*/
			public float Length(SeaRoutePoint p, int size_x) {
				return (float)Math.Sqrt(LengthSq(p, size_x));
			}
			public float Length(SeaRoutePoint p, int size_x, ref SeaRoutePoint near_p) {
				SeaRoutePoint near = null;
				return (float)Math.Sqrt(LengthSq(p, size_x, ref near));
			}

			/*-------------------------------------------------------------------------
			 ループを考慮した위치を작성함
			 単純にposition.x + size_x
			---------------------------------------------------------------------------*/
			private void build_loop_x(int size_x) {
				m_pos.X += size_x;
			}

			/*-------------------------------------------------------------------------
			 渡されたpointからの이동ベクトルを得る
			 ループは考慮されない
			 ベクトルは正規化されていない
			 正規化したベクトルを得るにはGetVectorNormalized()を사용する
			---------------------------------------------------------------------------*/
			public Vector2 GetVector(SeaRoutePoint from) {
				return new Vector2(this.Position.X - from.Position.X,
									this.Position.Y - from.Position.Y);
			}

			/*-------------------------------------------------------------------------
			 渡されたpointからの이동ベクトルを得る
			 ループは考慮されない
			 正規化されたベクトルを返す
			---------------------------------------------------------------------------*/
			public Vector2 GetVectorNormalized(SeaRoutePoint from) {
				Vector2 vec = GetVector(from);
				vec.Normalize();
				return vec;
			}

			/*-------------------------------------------------------------------------
			 渡されたpointからの이동ベクトルを得る
			 ループが考慮される
			 ベクトルは正規化されていない
			 正規化したベクトルを得るにはGetVectorNormalized()を사용する
			---------------------------------------------------------------------------*/
			public Vector2 GetVector(SeaRoutePoint from, int loop_x_size) {
				SeaRoutePoint near_p = null;
				from.LengthSq(this, loop_x_size, ref near_p);
				return near_p.GetVector(from);
			}

			/*-------------------------------------------------------------------------
			 渡されたpointからの이동ベクトルを得る
			 ループが考慮される
			 正規化されたベクトルを返す
			---------------------------------------------------------------------------*/
			public Vector2 GetVectorNormalized(SeaRoutePoint from, int loop_x_size) {
				Vector2 vec = GetVector(from, loop_x_size);
				vec.Normalize();
				return vec;
			}
		}

		/*-------------------------------------------------------------------------
		 말풍선좌표
		---------------------------------------------------------------------------*/
		public class SeaRoutePopupPoint : SeaRoutePoint {
			private int m_days;			 // 일수

			/*-------------------------------------------------------------------------

			---------------------------------------------------------------------------*/
			public int Days { get { return m_days; } }

			/*-------------------------------------------------------------------------

			---------------------------------------------------------------------------*/
			public SeaRoutePopupPoint(float x, float y, int color_index, int days)
				: base(x, y, color_index) {
				m_days = days;
			}
			public SeaRoutePopupPoint(Vector2 pos, int color_index, int days)
				: this(pos.X, pos.Y, color_index, days) {
			}
		}

		/*-------------------------------------------------------------------------
		 말풍선좌표
		 바운딩 박스区切り
		---------------------------------------------------------------------------*/
		public class SeaRoutePopupPointsBB : D3dBB2d {
			private List<SeaRoutePopupPoint> m_points;
			private int m_max_days;

			/*-------------------------------------------------------------------------

			---------------------------------------------------------------------------*/
			public List<SeaRoutePopupPoint> Points { get { return m_points; } }
			public int MaxDays { get { return m_max_days; } }

			/*-------------------------------------------------------------------------

			---------------------------------------------------------------------------*/
			public SeaRoutePopupPointsBB()
				: base() {
				m_points = new List<SeaRoutePopupPoint>();
				base.OffsetLT = new Vector2(-BB_OUTSIDESCREEEN_OFFSET, -BB_OUTSIDESCREEEN_OFFSET);
				base.OffsetRB = new Vector2(BB_OUTSIDESCREEEN_OFFSET, BB_OUTSIDESCREEEN_OFFSET);
				m_max_days = 0;
			}

			/*-------------------------------------------------------------------------
			 추가
			 바운딩 박스の사이즈が一定이상のときは추가せずfalseを返す
			---------------------------------------------------------------------------*/
			public bool Add(SeaRoutePopupPoint p) {
				Vector2 size = base.IfUpdate(p.Position).Size;
				if (size.X > BB_POPUP_SIZE_MAX) return false;
				if (size.Y > BB_POPUP_SIZE_MAX) return false;

				// 추가
				m_points.Add(p);
				base.Update(p.Position);

				// 최대항해일수の업데이트
				foreach (SeaRoutePopupPoint i in m_points) {
					if (i.Days > m_max_days) {
						m_max_days = i.Days;
					}
				}

				return true;		// 추가した
			}
		}

		/*-------------------------------------------------------------------------
		 항로도용라인
		---------------------------------------------------------------------------*/
		class RouteLineBB : D3dBB2d {
			private List<Vector2> m_points;
			private int m_color;			// alpha値は0なので그리기時注意

			/*-------------------------------------------------------------------------

			---------------------------------------------------------------------------*/
			public int Color { get { return m_color; } }
			public int Count { get { return m_points.Count; } }

			/*-------------------------------------------------------------------------

			---------------------------------------------------------------------------*/
			public RouteLineBB(int color) {
				m_points = new List<Vector2>();
				m_color = color;
				base.OffsetLT = new Vector2(-2, -2);
				base.OffsetRB = new Vector2(2, 2);
			}

			/*-------------------------------------------------------------------------
			 추가
			---------------------------------------------------------------------------*/
			public void AddPoint(Vector2 point) {
				m_points.Add(point);
				base.Update(point);
			}

			/*-------------------------------------------------------------------------
			 컬링정보を得る
			 trueを返したとき컬링する
			---------------------------------------------------------------------------*/
			public bool IsCulling(Vector2 offset, LoopXImage image) {
				return base.IsCulling(offset, image.ImageScale, new D3dBB2d.CullingRect(image.Device.client_size));
			}

			/*-------------------------------------------------------------------------
			 바운딩 박스を그리기する
			 デバッグ용
			---------------------------------------------------------------------------*/
			public void DrawBB(Vector2 offset, LoopXImage image) {
				// 그리기
				base.Draw(image.Device, 0.5f, offset, image.ImageScale, Color | (255 << 24));
			}

			/*-------------------------------------------------------------------------
			 그리기용に좌표변환された配열を得る
			 IsCulling()で컬링するかどうかを調べてからこの関수を呼ぶこと
			---------------------------------------------------------------------------*/
			public Vector2[] BuildLinePoints(Vector2 offset, LoopXImage image) {
				Vector2[] list = new Vector2[m_points.Count];
				int i = 0;
				foreach (Vector2 p in m_points) {
					list[i++] = image.GlobalPos2LocalPos(p, offset);
				}
				return list;
			}
		}

		/*-------------------------------------------------------------------------
		 1航해분の항로
		---------------------------------------------------------------------------*/
		public class Voyage {
			private gvt_lib m_lib;				  //

			private List<SeaRoutePoint> m_routes;			   // 항로
			private List<SeaRoutePopupPointsBB> m_popups;			   // 말풍선
			private List<SeaRoutePopupPointsBB> m_accidents;			// 재해
			private List<RouteLineBB> m_line_routes;			// 그리기용항로라인
			private bool m_is_build_line_routes;	// 항로라인を作ったらtrue

			private float m_alpha;			  // 전체に掛かる반투명具合
			private float m_gap_cos;				// できるだけ라인を繋げる각도(cos)

			private bool m_is_draw;			 // 그리기するときtrue
			private int m_max_days;			 // 최대항해일수
			private int m_minimum_draw_days;	// 그리기する최저항해일수
			private DateTime m_date;					// 항해시간
			private bool m_is_selected;		 // 항로도목록で선택されているときtrue

			/*-------------------------------------------------------------------------
			 
			---------------------------------------------------------------------------*/
			// 그리기時の반투명도
			public float Alpha {
				get { return m_alpha; }
				set {
					m_alpha = value;
					if (m_alpha < 0) m_alpha = 0;
					if (m_alpha > 1) m_alpha = 1;
				}
			}
			// 空かどうかを得る
			public bool IsEmpty {
				get {
					if (m_routes.Count > 1) return false;
					if (m_popups.Count > 1) return false;
					if (m_accidents.Count > 1) return false;
					return true;
				}
			}
			// 유저지정による그리기抑制
			public bool IsEnableDraw {
				get { return m_is_draw; }
				set { m_is_draw = value; }
			}

			// 최대항해일수
			public int MaxDays { get { return m_max_days; } }
			public string MaxDaysString { get { return String.Format("{0}일", MaxDays); } }

			// 그리기する최저항해일수
			public int MinimumDrawDays {
				get { return m_minimum_draw_days; }
				set { m_minimum_draw_days = value; }
			}

			// 出発地点
			public Vector2 MapPoint1st {
				get {
					if (m_routes.Count > 0) return m_routes[0].Position;
					return new Vector2(-1, -1);
				}
			}
			public string MapPoint1stString { get { return get_pos_str(MapPoint1st); } }
			public Point GamePoint1st {
				get {
					if (m_routes.Count > 0) {
						return transform.ToPoint(
									transform.map_pos2_game_pos(m_routes[0].Position, m_lib.loop_image));
					}
					return new Point(-1, -1);
				}
			}
			public string GamePoint1stStr { get { return get_pos_str(GamePoint1st); } }

			// 종료地点
			public Vector2 MapPointLast {
				get {
					if (m_routes.Count > 0) {
						return m_routes[m_routes.Count - 1].Position;
					}
					return new Vector2(-1, -1);
				}
			}
			public string MapPointLastString { get { return get_pos_str(MapPointLast); } }
			public Point GamePointLast {
				get {
					if (m_routes.Count > 0) {
						return transform.ToPoint(
									transform.map_pos2_game_pos(m_routes[m_routes.Count - 1].Position, m_lib.loop_image));
					}
					return new Point(-1, -1);
				}
			}
			public string GamePointLastString { get { return get_pos_str(GamePointLast); } }

			public DateTime DateTime { get { return m_date; } }
			public string DateTimeString {
				get {
					if (m_date.Ticks == 0) {
						//					if(m_date == null){
						return "불명(旧데이터)";
					}
					return Useful.TojbbsDateTimeString(m_date);
				}
			}
			public bool IsSelected {
				get { return m_is_selected; }
				set { m_is_selected = value; }
			}

			/*-------------------------------------------------------------------------
			 
			---------------------------------------------------------------------------*/
			public Voyage(gvt_lib lib) {
				m_lib = lib;

				m_popups = new List<SeaRoutePopupPointsBB>();
				m_accidents = new List<SeaRoutePopupPointsBB>();
				m_routes = new List<SeaRoutePoint>();
				m_line_routes = new List<RouteLineBB>();

				// 선の구축未完成
				m_is_build_line_routes = false;

				Alpha = 1;
				m_gap_cos = (float)Math.Cos(Useful.ToRadian(ADD_POINT_ANGLE_GAP_MAX));
				m_is_draw = true;
				m_max_days = 0;
				m_minimum_draw_days = 0;
				m_is_selected = false;

				m_date = DateTime.Now;
			}

			/*-------------------------------------------------------------------------
			 추가
			---------------------------------------------------------------------------*/
			public void AddPopup(SeaRoutePopupPoint p) {
				if (m_popups.Count <= 0) {
					m_popups.Add(new SeaRoutePopupPointsBB());
				}
				if (!m_popups[m_popups.Count - 1].Add(p)) {
					m_popups.Add(new SeaRoutePopupPointsBB());
					m_popups[m_popups.Count - 1].Add(p);
				}

				// 최대항해일수の업데이트
				foreach (SeaRoutePopupPointsBB i in m_popups) {
					if (i.MaxDays > m_max_days) m_max_days = i.MaxDays;
				}
			}
			public void AddAccident(SeaRoutePopupPoint p) {
				if (m_accidents.Count <= 0) {
					m_accidents.Add(new SeaRoutePopupPointsBB());
				}
				if (!m_accidents[m_accidents.Count - 1].Add(p)) {
					m_accidents.Add(new SeaRoutePopupPointsBB());
					m_accidents[m_accidents.Count - 1].Add(p);
				}
			}
			public void AddPoint(SeaRoutePoint p) {
				m_routes.Add(p);
				m_is_build_line_routes = false; // 항로선구축リクエスト
			}

			/*-------------------------------------------------------------------------
			 항로도の라인を구축する
			 ある程도集まった라인を작성함
			 좌표間の距離によって라인とするかどうかを判断する
			 바운딩 박스の사이즈が대きくなった場合선を분ける
			 좌표間の距離が대きすぎる場合は각도差によって라인にするかを決定する
			---------------------------------------------------------------------------*/
			private void build_line_routes() {
				if (m_is_build_line_routes) return;	 // 作ってある

				m_line_routes.Clear();				  // 破棄
				if (m_routes.Count < 2) return;	 // 선を作れない

				SeaRoutePoint old_pos = m_routes[0];	// 최초の좌표
				SeaRoutePoint old_pos2 = null;		  // 2つ前の좌표
				RouteLineBB route = null;

				for (int i = 1; i < m_routes.Count; i++) {
					if (route == null) {
						// 開始
						route = new RouteLineBB(old_pos.Color);
						route.AddPoint(old_pos.Position);
					}

					// ループを考慮した距離の2乗を得る
					// near_p は近い위치を返す
					// ループしない場合が最短の場合は near_p == m_routes[i]
					SeaRoutePoint near_p = null;
					float length = old_pos.LengthSq(m_routes[i],
														(int)m_lib.loop_image.ImageSize.X,
														ref near_p);

					// 距離判定
					if (length > LINE_ROUTE_MAX) {
#if DISABLE_SEA_ROUTES_CHAIN_POINTS
						// 出来るだけ繋げる処理무효
						near_p	= null;
#else
						if (length > ADD_POINT_ANGLE_LENGTH_SQ_MAX) {
							// 距離が遠すぎる
							near_p = null;
						} else {
							// 각도差判定
							// 각도差がありすぎるときはnullが返される
							near_p = check_point_sub(old_pos, old_pos2, near_p);
						}
#endif
					}

					// 추가
					if (near_p != null) route.AddPoint(near_p.Position);

					// 바운딩 박스の사이즈체크
					// 분할플래그か사이즈が대きすぎるとき분할する
					if ((near_p != m_routes[i])			 // ループを考慮した위치を추가したか, 추가されなかった
						|| (route.Size.X >= BB_SIZE_MAX)			// 바운딩 박스の사이즈を超えた
						|| (route.Size.Y >= BB_SIZE_MAX)) {	 // 바운딩 박스の사이즈を超えた
																// 선を区切る
						add_route(route);
						route = null;
					}

					// 以前の좌표を업데이트する
					old_pos2 = old_pos;
					old_pos = m_routes[i];
				}

				// 추가残りがあれば추가する
				if (route != null) add_route(route);

				// 작성완료
				m_is_build_line_routes = true;
			}

			/*-------------------------------------------------------------------------
			 距離が遠い場合の체크
			 각도差で추가するかどうかを決める
			 각도差がありすぎるときはnullを返す
			---------------------------------------------------------------------------*/
			private SeaRoutePoint check_point_sub(SeaRoutePoint old_pos1, SeaRoutePoint old_pos2, SeaRoutePoint new_pos) {
				if (old_pos1 == null) return null;
				if (old_pos2 == null) return null;

				// 이동ベクトルを得る
				// ループが考慮される
				Vector2 old_vec = old_pos1.GetVectorNormalized(old_pos2, (int)m_lib.loop_image.ImageSize.X);
				Vector2 new_vec = new_pos.GetVectorNormalized(old_pos1, (int)m_lib.loop_image.ImageSize.X);

				// 각도差(cos)を得る
				float gap = Vector2.Dot(old_vec, new_vec);
				if (gap >= m_gap_cos) {
					return new_pos;
				}
				return null;
			}

			/*-------------------------------------------------------------------------
			 선を목록に추가
			---------------------------------------------------------------------------*/
			private void add_route(RouteLineBB route) {
				if (route.Count < 2) return;
				m_line_routes.Add(route);
			}

			/*-------------------------------------------------------------------------
			 항로도그리기
			---------------------------------------------------------------------------*/
			public void DrawRoutes(Vector2 offset, LoopXImage image, bool is_select_mode) {
				// 그리기する必要があるか체크
				if (!can_draw(is_select_mode)) return;

				// 라인を구축
				// 작성済みであればなにもしない
				// 그리기する必要がある場合のみ구축を試みる
				build_line_routes();

				// 반투명の値を得る
				int alpha = (int)(m_alpha * 255);
				if (m_is_selected) alpha = 255;
				alpha <<= 24;

				// 그리기
				foreach (RouteLineBB line in m_line_routes) {
					// 바운딩 박스で컬링
					if (line.IsCulling(offset, image)) continue;

					// 좌표변환후그리기
					m_lib.device.line.Draw(line.BuildLinePoints(offset, image), line.Color | alpha);
				}
			}

			/*-------------------------------------------------------------------------
			 그리기するか체크
			 선택모드중の선택없음
			 그리기플래그
			 항해일수が그리기する최저항해일수に満たない
			---------------------------------------------------------------------------*/
			private bool can_draw(bool is_select_mode) {
				// 선택時は無条건で그리기
				if (m_is_selected) return true;
				// 선택모드중で선택중でない
				if (is_select_mode && (!m_is_selected)) return false;

				// 비표시時はなにもしない
				if (!m_is_draw) return false;
				// 항해일수が그리기する최저항해일수に満たないなら그리지않음
				if (MaxDays < MinimumDrawDays) return false;

				// 그리기する
				return true;
			}

			/*-------------------------------------------------------------------------
			 바운딩 박스그리기
			 デバッグ용
			---------------------------------------------------------------------------*/
			public void DrawRoutesBB(Vector2 offset, LoopXImage image, bool is_select_mode) {
				// 그리기する必要があるか체크
				if (!can_draw(is_select_mode)) return;

				foreach (RouteLineBB line in m_line_routes) {
					line.DrawBB(offset, image);
				}
			}

			/*-------------------------------------------------------------------------
			 말풍선그리기
			---------------------------------------------------------------------------*/
			public void DrawPopups(Vector2 offset, LoopXImage image, bool is_select_mode) {
				// 그리기する必要があるか체크
				if (!can_draw(is_select_mode)) return;

				float size = image.ImageScale;
				if (size < 0.5) size = 0.5f;
				else if (size > 1) size = 1;

				// 반투명具合を反映
				float alpha = (m_is_selected) ? 1 : m_alpha;
				int alpha1 = ((int)(alpha * 255)) << 24;
				int alpha2 = ((int)(alpha * 64)) << 24;
				int color1 = 0x00ffffff | alpha1;
				int color2 = 0x00ffffff | alpha2;

				D3dBB2d.CullingRect rect = new D3dBB2d.CullingRect(image.Device.client_size);
				foreach (SeaRoutePopupPointsBB b in m_popups) {
					// 바운딩 박스で화면외かどうか조사
					if (b.IsCulling(offset, image.ImageScale, rect)) {
						continue;
					}
#if DRAW_POPUPS_BOUNDINGBOX
					d3d_bb2d.Draw(b.bb, image.device, 0.5f, offset, image.scale, Color.Blue.ToArgb());
#endif
					foreach (SeaRoutePopupPoint p in b.Points) {
						// 일付말풍선
						if (p.ColorIndex < 0) continue;
						if (p.ColorIndex >= 8) continue;

						Vector3 pos = new Vector3(p.Position.X, p.Position.Y, 0.5f);

						if ((p.Days % m_lib.setting.draw_popup_day_interval) != 0) {
							// 통상の소さい아이콘
							m_lib.device.sprites.AddDrawSpritesNC(pos,
													m_lib.icons.GetIcon(icons.icon_index.days_mini_6),
													new Vector2(size, size),
													alpha1 | p.Color);
						} else {
							// 일付말풍선

							// 影
							pos.Z = 0.8f;
							m_lib.device.sprites.AddDrawSpritesNC(pos,
													m_lib.icons.GetIcon(icons.icon_index.days_big_shadow),
													new Vector2(1, 1),
													color2);
							pos.Z = 0.5f;
							// 3桁の場合は가로に広い絵を사용する
							icons.icon_index icon = (p.Days >= 100) ? icons.icon_index.days_big_100 : icons.icon_index.days_big_6;
							// フキダシ
							if (m_lib.device.sprites.AddDrawSpritesNC(pos, m_lib.icons.GetIcon(icon), alpha1 | p.Color)) {
								// 말풍선が컬링されなかったら일수を描く
								// 4桁이상は캡처できないと思う, たぶん
								int days = p.Days;
								if (days > 999) days = 999;
								if (days < 0) days = 0;

								int max = 1;
								Vector2 offset2 = new Vector2(0, 0);
								if (days >= 100) {	  // 3桁
									offset2.X += 7;
									max = 3;
								} else if (days >= 10) {	// 2桁
									offset2.X += 4;
									max = 2;
								}
								// 일수
								for (int i = 0; i < max; i++) {
									m_lib.device.sprites.AddDrawSpritesNC(pos, m_lib.icons.GetIcon(icons.icon_index.number_0 + (days % 10)), color1, offset2);
									offset2.X -= 7;
									days /= 10;
								}
							}
						}
					}
				}
			}

			/*-------------------------------------------------------------------------
			 재해그리기
			---------------------------------------------------------------------------*/
			public void DrawAccidents(Vector2 offset, LoopXImage image, bool is_select_mode) {
				// 그리기する必要があるか체크
				if (!can_draw(is_select_mode)) return;

				// 반투명具合を反映
				float alpha = (m_is_selected) ? 1 : m_alpha;
				int alpha1 = ((int)(alpha * 255)) << 24;
				int alpha2 = ((int)(alpha * 64)) << 24;
				int color1 = 0x00ffffff | alpha1;
				int color2 = 0x00ffffff | alpha2;

				D3dBB2d.CullingRect rect = new D3dBB2d.CullingRect(image.Device.client_size);
				foreach (SeaRoutePopupPointsBB b in m_accidents) {
					// 바운딩 박스で화면외かどうか조사
					if (b.IsCulling(offset, image.ImageScale, rect)) {
						continue;
					}
#if DRAW_POPUPS_BOUNDINGBOX
					d3d_bb2d.Draw(b.bb, image.device, 0.5f, offset, image.scale, Color.Red.ToArgb());
#endif

					foreach (SeaRoutePopupPoint p in b.Points) {
						if (p.ColorIndex < 101) continue;
						if (p.ColorIndex > 111) continue;
						if (!is_draw_popups(p.ColorIndex)) continue;

						Vector3 pos = new Vector3(p.Position.X, p.Position.Y, 0.5f);

						// 影
						pos.Z = 0.8f;
						m_lib.device.sprites.AddDrawSpritesNC(pos,
												m_lib.icons.GetIcon(icons.icon_index.accident_popup_shadow),
												new Vector2(1, 1),
												color2);

						// フキダシ
						pos.Z = 0.5f;
						m_lib.device.sprites.AddDrawSpritesNC(pos,
									m_lib.icons.GetIcon(icons.icon_index.accident_popup), color1);
						// 내용を描く
						m_lib.device.sprites.AddDrawSpritesNC(pos,
									m_lib.icons.GetIcon(icons.icon_index.accident_0 + (p.ColorIndex - 101)), color1);
					}
				}
			}

			/*-------------------------------------------------------------------------
			 표시항목체크
			---------------------------------------------------------------------------*/
			private bool is_draw_popups(int index) {
				// 그리기플래그
				DrawSettingAccidents flag = m_lib.setting.draw_setting_accidents;

				switch (index) {
					case 101:
						if ((flag & DrawSettingAccidents.accident_0) == 0) return false;
						break;
					case 102:
						if ((flag & DrawSettingAccidents.accident_1) == 0) return false;
						break;
					case 103:
						if ((flag & DrawSettingAccidents.accident_2) == 0) return false;
						break;
					case 104:
						if ((flag & DrawSettingAccidents.accident_3) == 0) return false;
						break;
					case 105:
						if ((flag & DrawSettingAccidents.accident_4) == 0) return false;
						break;
					case 106:
						if ((flag & DrawSettingAccidents.accident_5) == 0) return false;
						break;
					case 107:
						if ((flag & DrawSettingAccidents.accident_6) == 0) return false;
						break;
					case 108:
						if ((flag & DrawSettingAccidents.accident_7) == 0) return false;
						break;
					case 109:
						if ((flag & DrawSettingAccidents.accident_8) == 0) return false;
						break;
					case 110:
						if ((flag & DrawSettingAccidents.accident_9) == 0) return false;
						break;
					case 111:
						if ((flag & DrawSettingAccidents.accident_10) == 0) return false;
						break;
				}
				return true;
			}

			/*-------------------------------------------------------------------------
			 읽기開始
			 항해시간を초기화する
			---------------------------------------------------------------------------*/
			public void StartLoad() {
				m_date = new DateTime();
			}

			/*-------------------------------------------------------------------------
			 읽기
			---------------------------------------------------------------------------*/
			public void LoadFromLine(string line) {
				try {
					string[] split = line.Split(new char[] { ',' });

					switch (split[0]) {
						case "popup":
						case "accidents": {
								int x = Convert.ToInt32(split[1]);
								int y = Convert.ToInt32(split[2]);
								int days = Convert.ToInt32(split[3]);
								int color_index = Convert.ToInt32(split[4]);

								// 추가
								if ((color_index >= 101) && (color_index <= 111)) {
									// 재해
									AddAccident(new SeaRoutePopupPoint(x, y, color_index, days));
								} else {
									// 일付
									AddPopup(new SeaRoutePopupPoint(x, y, color_index, days));
								}
							}
							break;
						case "routes": {
								float x = (float)Convert.ToDouble(split[1]);
								float y = (float)Convert.ToDouble(split[2]);
								int color_index = Convert.ToInt32(split[3]);

								// 추가
								m_routes.Add(new SeaRoutePoint(x, y, color_index));
							}
							break;
						case "draw": {
								int flag = Convert.ToInt32(split[1]);
								m_is_draw = (flag != 0) ? true : false;
							}
							break;
						case "date": {
								m_date = Useful.ToDateTime(split[1]);
							}
							break;
						default:
							break;
					}
				} catch {
					// 읽기실패
					// この行は無視される
				}
			}

			/*-------------------------------------------------------------------------
			 書き出し
			---------------------------------------------------------------------------*/
			public void Write(StreamWriter sw) {
				if (IsEnableDraw) sw.WriteLine("draw,1");
				else sw.WriteLine("draw,0");
				sw.WriteLine("date," + Useful.TojbbsDateTimeString(m_date));

				foreach (SeaRoutePopupPointsBB b in m_popups) {
					foreach (SeaRoutePopupPoint p in b.Points) {
						string str = "";
						str += "popup,";
						str += ((int)p.Position.X).ToString() + ",";
						str += ((int)p.Position.Y).ToString() + ",";
						str += p.Days + ",";
						str += p.ColorIndex;
						sw.WriteLine(str);
					}
				}
				foreach (SeaRoutePopupPointsBB b in m_accidents) {
					foreach (SeaRoutePopupPoint p in b.Points) {
						string str = "";
						str += "accidents,";
						str += ((int)p.Position.X).ToString() + ",";
						str += ((int)p.Position.Y).ToString() + ",";
						str += p.Days + ",";
						str += p.ColorIndex;
						sw.WriteLine(str);
					}
				}

				foreach (SeaRoutePoint p in m_routes) {
					string str = "";
					str += "routes,";
					str += p.Position.X + ",";
					str += p.Position.Y + ",";
					str += p.ColorIndex;
					sw.WriteLine(str);
				}
			}

			/*-------------------------------------------------------------------------
			 그리기용の색번호を得る
			---------------------------------------------------------------------------*/
			public int GetColorIndex() {
				if (m_routes.Count > 0) {
					return m_routes[0].ColorIndex;
				}
				if (m_popups.Count > 0) {
					if (m_popups[0].Points.Count > 0) {
						return m_popups[0].Points[0].ColorIndex;
					}
				}
				return 0;
			}

			/*-------------------------------------------------------------------------
			 삭제
			---------------------------------------------------------------------------*/
			public void Remove(bool popups, bool accident, bool routes) {
				if (popups) m_popups.Clear();
				if (accident) m_accidents.Clear();
				if (routes) {
					m_routes.Clear();
					m_line_routes.Clear();
				}
			}

			/*-------------------------------------------------------------------------
			 스크린샷용
			 含まれる범위を등록する
			---------------------------------------------------------------------------*/
			public void SS_AddMinMaxList(List<Point>[] map, ref int min_y, ref int max_y, bool is_select_mode) {
				// 그리기する必要があるか체크
				if (!can_draw(is_select_mode)) return;

				foreach (SeaRoutePoint p in m_routes) {
					add_minmax_list(map,
									transform.ToPoint(p.Position),
									ref min_y, ref max_y);
				}
				foreach (SeaRoutePopupPointsBB b in m_popups) {
					foreach (SeaRoutePopupPoint p in b.Points) {
						add_minmax_list(map,
										transform.ToPoint(p.Position),
										ref min_y, ref max_y);
					}
				}
				foreach (SeaRoutePopupPointsBB b in m_accidents) {
					foreach (SeaRoutePopupPoint p in b.Points) {
						add_minmax_list(map,
										transform.ToPoint(p.Position),
										ref min_y, ref max_y);
					}
				}
			}

			/*-------------------------------------------------------------------------
			 X방향はおおまかな区切りに등록する
			 Y방향はそのまま최대最소を得る
			---------------------------------------------------------------------------*/
			private void add_minmax_list(List<Point>[] map, Point pos, ref int min_y, ref int max_y) {
				calc_bounding_box_y(pos.Y, ref min_y, ref max_y);
				int index = pos.X / SS_DISTRIBUTION_X;
				if (index < 0) return;
				if (index >= map.Length) return;
				map[index].Add(pos);
			}

			/*-------------------------------------------------------------------------
			 스크린샷용に바운딩 박스を求める
			 세로방향
			---------------------------------------------------------------------------*/
			private void calc_bounding_box_y(int y, ref int min, ref int max) {
				if (y <= 64) return;
				if (y > max) max = y;
				if (y < min) min = y;
			}

			/*-------------------------------------------------------------------------
			 위치を문자열で得る
			---------------------------------------------------------------------------*/
			private string get_pos_str(Point pos) {
				if ((pos.X < 0)
					|| (pos.Y < 0)) {
					return "불명의위치";
				}
				return String.Format("{0},{1}", pos.X, pos.Y);
			}
			private string get_pos_str(Vector2 pos) {
				if ((pos.X < 0)
					|| (pos.Y < 0)) {
					return "불명의위치";
				}
				return String.Format("{0},{1}", (int)pos.X, (int)pos.Y);
			}
		}

		/*-------------------------------------------------------------------------

		---------------------------------------------------------------------------*/
		private gvt_lib m_lib;				  //

		private List<Voyage> m_sea_routes;		  // 항로목록
													// 색が変わるタイミングで区切られる
		private List<Voyage> m_favorite_sea_routes; // 항로도즐겨찾기목록
		private List<Voyage> m_trash_sea_routes;		// ごみ箱항로목록

		// 추가時용
		private int m_color_index;
		private int m_old_days;
		private Point m_old_day_pos;
		private Point m_old_pos;
		private bool m_is_1st;

		// 선택중は선택されている항로도のみ그리기する
		private bool m_is_select_mode;

		private RequestCtrl m_req_update_list;	  // 항로도목록업데이트リクエスト
		private RequestCtrl m_req_redraw_list;	  // 항로도목록다시그리기リクエスト

		/*-------------------------------------------------------------------------

		---------------------------------------------------------------------------*/
		public List<Voyage> searoutes { get { return m_sea_routes; } }
		public List<Voyage> favorite_sea_routes { get { return m_favorite_sea_routes; } }
		public List<Voyage> trash_sea_routes { get { return m_trash_sea_routes; } }
		public RequestCtrl req_update_list { get { return m_req_update_list; } }
		public RequestCtrl req_redraw_list { get { return m_req_redraw_list; } }

		/*-------------------------------------------------------------------------

		---------------------------------------------------------------------------*/
		public SeaRoutes(gvt_lib lib, string file_name, string favorite_file_name, string trash_file_name) {
			m_lib = lib;

			m_sea_routes = new List<Voyage>();
			m_favorite_sea_routes = new List<Voyage>();
			m_trash_sea_routes = new List<Voyage>();
			m_req_update_list = new RequestCtrl();
			m_req_redraw_list = new RequestCtrl();

			// 선택모드
			m_is_select_mode = false;

			// 추가초기화
			init_add_points();

			// 以前の버전の읽기
			load_old_routes(def.SEAROUTE_FULLFNAME1, def.SEAROUTE_FULLFNAME2);

			// 항로도읽기
			load_routes(file_name);
			// 즐겨찾기항로도읽기
			load_routes_sub(m_favorite_sea_routes, favorite_file_name);
			// ごみ箱항로도읽기
			load_routes_sub(m_trash_sea_routes, trash_file_name);
		}

		/*-------------------------------------------------------------------------
		 以前の버전の읽기
		---------------------------------------------------------------------------*/
		private bool load_old_routes(string file_name1, string file_name2) {
			// どちらもなければなにもしない
			// 初회時に読み込んだ후は삭제される
			if ((!File.Exists(file_name1))
				&& (!File.Exists(file_name2))) {
				return true;
			}

			string line = "";
			int old_color_index = -1;

			// 항로도
			try {
				using (StreamReader sr = new StreamReader(
					file_name2, Encoding.GetEncoding("UTF-8"))) {

					while ((line = sr.ReadLine()) != null) {
						try {
							string[] split = line.Split(new char[] { ',' });

							int x = Convert.ToInt32(split[0]);
							int y = Convert.ToInt32(split[1]);
							int color_index = Convert.ToInt32(split[2]);

							// 추가
							if (old_color_index != color_index) {
								// 색が変わったら그룹を추가
								add_sea_routes();
								old_color_index = color_index;
							}
							add_point(new SeaRoutePoint(x, y, color_index));
						} catch {
						}
					}
				}
			} catch {
				// 읽기실패
				m_sea_routes.Clear();
			}

			// ポップアップ
			// トラブル회避のため, 최신の장소に전부로드
			try {
				using (StreamReader sr = new StreamReader(
					file_name1, Encoding.GetEncoding("UTF-8"))) {

					while ((line = sr.ReadLine()) != null) {
						try {
							string[] split = line.Split(new char[] { ',' });

							int x = Convert.ToInt32(split[0]);
							int y = Convert.ToInt32(split[1]);
							int days = Convert.ToInt32(split[2]);
							int color_index = Convert.ToInt32(split[3]);

							// 추가
							if ((color_index >= 101) && (color_index <= 111)) {
								// 재해
								add_accident(new SeaRoutePopupPoint(x, y, color_index, days));
							} else {
								// 일付
								add_popup(new SeaRoutePopupPoint(x, y, color_index, days));
							}
						} catch {
						}
					}
				}
			} catch {
				// 읽기실패
				m_sea_routes.Clear();
			}

			// 고い버전の파일は삭제する
			file_ctrl.RemoveFile(file_name1);
			file_ctrl.RemoveFile(file_name2);
			return true;
		}

		/*-------------------------------------------------------------------------
		 읽기
		---------------------------------------------------------------------------*/
		private bool load_routes(string file_name) {
			// 읽기
			if (!load_routes_sub(m_sea_routes, file_name)) return false;

			// 新規に추가するときの색を決定する
			if (m_sea_routes.Count > 1) {
				m_color_index = get_newest_sea_routes().GetColorIndex();
				if (m_color_index < 0) m_color_index = 0;
				if (++m_color_index >= 8) m_color_index = 0;
			}
			return true;
		}

		/*-------------------------------------------------------------------------
		 읽기
		 sub
		---------------------------------------------------------------------------*/
		private bool load_routes_sub(List<Voyage> list, string file_name) {
			string line = "";
			try {
				using (StreamReader sr = new StreamReader(
					file_name, Encoding.GetEncoding("UTF-8"))) {

					while ((line = sr.ReadLine()) != null) {
						if (line == "start routes") {
							add_sea_routes(list);
							get_newest_sea_routes(list).StartLoad();
						} else {
							get_newest_sea_routes(list).LoadFromLine(line);
						}
					}
				}
			} catch {
				// 읽기실패
				return false;
			}
			return true;
		}

		/*-------------------------------------------------------------------------
		 최신の항로정보を得る
		---------------------------------------------------------------------------*/
		private Voyage get_newest_sea_routes() {
			return get_newest_sea_routes(m_sea_routes);
		}
		private Voyage get_newest_sea_routes(List<Voyage> list) {
			if (list.Count < 1) {
				// 無いので作る
				add_sea_routes(list);
			}
			return list[list.Count - 1];
		}

		/*-------------------------------------------------------------------------
		 항로정보を추가する
		---------------------------------------------------------------------------*/
		private void add_sea_routes() {
			add_sea_routes(m_sea_routes);
		}
		private void add_sea_routes(List<Voyage> list) {
			list.Add(new Voyage(m_lib));

			// 목록업데이트リクエスト
			m_req_update_list.Request();
		}

		/*-------------------------------------------------------------------------
		 保持수조정
		---------------------------------------------------------------------------*/
		private void ajust_size() {
			// 항로도목록조정
			if (m_sea_routes.Count > 0) {
				while (m_sea_routes.Count > m_lib.setting.searoutes_group_max) {
					Voyage once = m_sea_routes[0];
					m_sea_routes.RemoveAt(0);
					// 과거의항로도목록に이동させる
					m_trash_sea_routes.Add(once);

					// 목록업데이트リクエスト
					m_req_update_list.Request();
				}
			}
			// 과거의항로도목록조정
			while (m_trash_sea_routes.Count > m_lib.setting.trash_searoutes_group_max) {
				m_trash_sea_routes.RemoveAt(0);

				// 목록업데이트リクエスト
				m_req_update_list.Request();
			}
		}

		/*-------------------------------------------------------------------------
		 그리기
		 항로도
		---------------------------------------------------------------------------*/
		public void DrawRoutesLines() {
			// 保持수조정
			ajust_size();

			// 선택모드かどうかを判断する
			check_select_mode();

			// 반투명具合を反映させる
			set_alpha();
			// 그리기する최저항해일수を反映させる
			set_minimum_draw_days();

			// 항로도그리기
			draw_routes();
		}

		/*-------------------------------------------------------------------------
		 항로도を전부열挙する
		---------------------------------------------------------------------------*/
		private IEnumerable<Voyage> enum_all_sea_routes() {
			foreach (Voyage p in m_trash_sea_routes) yield return p;
			foreach (Voyage p in m_favorite_sea_routes) yield return p;
			foreach (Voyage p in m_sea_routes) yield return p;
		}

		/*-------------------------------------------------------------------------
		 항로도を열挙する
		 ごみ箱は열挙されない
		---------------------------------------------------------------------------*/
		private IEnumerable<Voyage> enum_sea_routes_without_trash() {
			foreach (Voyage p in m_favorite_sea_routes) yield return p;
			foreach (Voyage p in m_sea_routes) yield return p;
		}

		/*-------------------------------------------------------------------------
		 선택모드중かどうかを判断する
		---------------------------------------------------------------------------*/
		private void check_select_mode() {
			m_is_select_mode = false;

			IEnumerable<Voyage> list = enum_all_sea_routes();
			foreach (Voyage p in list) {
				if (p.IsSelected) {
					m_is_select_mode = true;
					break;
				}
			}
		}

		/*-------------------------------------------------------------------------
		 그리기
		 吹き出し
		 DrawRoutesLines()を呼んだ후であること
		---------------------------------------------------------------------------*/
		public void DrawPopups() {
			// 재해그리기
			draw_accident();
			// 말풍선그리기
			draw_popups();
		}

		/*-------------------------------------------------------------------------
		 반투명具合を지정する
		 설정により, 최신の항로以외を薄くする
		---------------------------------------------------------------------------*/
		private void set_alpha() {
			if (m_lib.setting.enable_sea_routes_aplha) {
				// 최신以외を薄くする
				foreach (Voyage p in m_sea_routes) {
					p.Alpha = SEAROUTES_ALPHA;
				}
				// 최신は不투명
				if (m_sea_routes.Count > 0) {
					get_newest_sea_routes().Alpha = 1;
				}
			} else {
				// 전부不투명
				foreach (Voyage p in m_sea_routes) {
					p.Alpha = 1;
				}
			}

			if (m_lib.setting.enable_favorite_sea_routes_alpha) {
				foreach (Voyage p in m_favorite_sea_routes) {
					p.Alpha = SEAROUTES_ALPHA;
				}
			} else {
				foreach (Voyage p in m_favorite_sea_routes) {
					p.Alpha = 1;
				}
			}
		}

		/*-------------------------------------------------------------------------
		 그리기する최저항해일수を反映させる
		---------------------------------------------------------------------------*/
		private void set_minimum_draw_days() {
			foreach (Voyage p in m_sea_routes) {
				p.MinimumDrawDays = m_lib.setting.minimum_draw_days;
			}
			// 최신は必ず그리기
			if (m_sea_routes.Count > 0) {
				get_newest_sea_routes().MinimumDrawDays = 0;
			}
		}

		/*-------------------------------------------------------------------------
		 항로도그리기
		---------------------------------------------------------------------------*/
		private void draw_routes() {
			if (!m_lib.setting.draw_sea_routes) return;

			float size = 1 * m_lib.loop_image.ImageScale;
			if (size < 1) size = 1;
			else if (size > 2) size = 2;

			// 선택모드중は太く그리기
			if (m_is_select_mode) size *= 3;

			m_lib.device.line.Width = size;
			m_lib.device.line.Antialias = m_lib.setting.enable_line_antialias;
			m_lib.device.line.Pattern = -1;
			m_lib.device.line.PatternScale = 1.0f;

			m_lib.device.device.RenderState.ZBufferEnable = false;
			m_lib.device.line.Begin();
			m_lib.loop_image.EnumDrawCallBack(new LoopXImage.DrawHandler(draw_routes_proc), 8f);
			m_lib.device.line.End();

#if DRAW_SEA_ROUTES_BOUNDINGBOX
			// debug
			// 바운딩 박스그리기
			m_lib.loop_image.EnumDrawCallBack(new loop_x_image.DrawHandler(draw_routes_bb_proc), 8f); 
#endif
			m_lib.device.device.RenderState.ZBufferEnable = true;
		}

		/*-------------------------------------------------------------------------
		 항로도그리기
		---------------------------------------------------------------------------*/
		private void draw_routes_proc(Vector2 offset, LoopXImage image) {
			IEnumerable<Voyage> list = (m_is_select_mode)
														? enum_all_sea_routes()
														: enum_sea_routes_without_trash();
			foreach (Voyage p in list) {
				p.DrawRoutes(offset, image, m_is_select_mode);
			}
		}

		/*-------------------------------------------------------------------------
		 바운딩 박스그리기
		 デバッグ용
		---------------------------------------------------------------------------*/
		private void draw_routes_bb_proc(Vector2 offset, LoopXImage image) {
			IEnumerable<Voyage> list = (m_is_select_mode)
														? enum_all_sea_routes()
														: enum_sea_routes_without_trash();
			foreach (Voyage p in list) {
				p.DrawRoutesBB(offset, image, m_is_select_mode);
			}
		}

		/*-------------------------------------------------------------------------
		 말풍선그리기
		---------------------------------------------------------------------------*/
		private void draw_popups() {
			if (m_lib.setting.draw_popup_day_interval == 0) return;
			m_lib.loop_image.EnumDrawCallBack(new LoopXImage.DrawHandler(draw_popups_proc), 32f);
		}

		/*-------------------------------------------------------------------------
		 말풍선그리기
		---------------------------------------------------------------------------*/
		private void draw_popups_proc(Vector2 offset, LoopXImage image) {
			m_lib.device.sprites.BeginDrawSprites(m_lib.icons.texture, offset, image.ImageScale, new Vector2(1, 1));
			IEnumerable<Voyage> list = (m_is_select_mode)
														? enum_all_sea_routes()
														: enum_sea_routes_without_trash();
			foreach (Voyage p in list) {
				p.DrawPopups(offset, image, m_is_select_mode);
			}
			m_lib.device.sprites.EndDrawSprites();
		}

		/*-------------------------------------------------------------------------
		 재해그리기
		---------------------------------------------------------------------------*/
		private void draw_accident() {
			if (!m_lib.setting.draw_accident) return;

			m_lib.loop_image.EnumDrawCallBack(new LoopXImage.DrawHandler(draw_accident_proc), 32f);
		}

		/*-------------------------------------------------------------------------
		 재해그리기
		---------------------------------------------------------------------------*/
		private void draw_accident_proc(Vector2 offset, LoopXImage image) {
			m_lib.device.sprites.BeginDrawSprites(m_lib.icons.texture, offset, image.ImageScale, new Vector2(1, 1));

			if (m_is_select_mode) {
				IEnumerable<Voyage> list = enum_all_sea_routes();
				foreach (Voyage p in list) {
					p.DrawAccidents(offset, image, m_is_select_mode);
				}
			} else {
				if (m_lib.setting.draw_favorite_sea_routes_alpha_popup) {
					// 즐겨찾기항로도の재해ポップアップを그리기する
					IEnumerable<Voyage> list = enum_sea_routes_without_trash();
					foreach (Voyage p in list) {
						p.DrawAccidents(offset, image, m_is_select_mode);
					}
				} else {
					// 즐겨찾기항로도の재해ポップアップを그리지않음
					foreach (Voyage p in m_sea_routes) {
						p.DrawAccidents(offset, image, m_is_select_mode);
					}
				}
			}
			m_lib.device.sprites.EndDrawSprites();
		}

		/*-------------------------------------------------------------------------
		 좌표추가関係
		---------------------------------------------------------------------------*/
		private void init_add_points() {
			m_color_index = 0;
			m_old_days = 0;
			m_old_day_pos = new Point(0, 0);
			m_old_pos = new Point(0, 0);
			m_is_1st = true;
		}

		/*-------------------------------------------------------------------------
		 항로도の추가
		 距離が近い場合は추가しない
		 일付が前회より소さいと新しい색に変わる
		 posには측량좌표を渡すこと
		---------------------------------------------------------------------------*/
		public void AddPoint(Point pos, int days, int accident) {
			if (days < 0) return;
			if (pos.X < 0) return;
			if (pos.Y < 0) return;

			// 지도좌표に변환
			Vector2 fpos = transform.game_pos2_map_pos(transform.ToVector2(pos), m_lib.loop_image);

			if (m_is_1st) {
				// 初めて
				add_sea_routes();			   // 추가
				m_old_days = days;
				m_old_pos = pos;
				if (days > 0) {
					add_popup(new SeaRoutePopupPoint(fpos, m_color_index, days));
				}
				// 추가のみで라인を구축しない
				add_point(new SeaRoutePoint(fpos, m_color_index));
				m_is_1st = false;
			} else {
				// 2회目以降
				if (m_old_days != days) {
					// 前회と同じ일付ではない
					if (m_old_days > days) {
						// 一도寄港した
						if (++m_color_index >= 8) m_color_index = 0;
						add_sea_routes();			   // 추가
					}
					// 0일は추가しない
					if (days > 0) add_popup(new SeaRoutePopupPoint(fpos, m_color_index, days));
					m_old_days = days;
				}
				// 항로도
				if (m_old_pos != pos) {
					// 前회と위치が違う
					// 距離が近すぎる場合は추가しない
					SeaRoutePoint p1 = new SeaRoutePoint(transform.ToVector2(m_old_pos));
					SeaRoutePoint p2 = new SeaRoutePoint(transform.ToVector2(pos));
					float min = ADD_POINT_MIN * transform.get_rate_to_game_x(m_lib.loop_image);
					if (p1.LengthSq(p2, (int)m_lib.loop_image.ImageSize.X) >= min) {
						add_point(new SeaRoutePoint(fpos, m_color_index));
						m_old_pos = pos;
					}
				}
			}

			// 재해
			if ((accident >= 101) && (accident <= 111)) {
				add_accident(new SeaRoutePopupPoint(fpos, accident, days));
			}
		}

		/*-------------------------------------------------------------------------
		 popup を추가する
		---------------------------------------------------------------------------*/
		private void add_popup(SeaRoutePopupPoint p) {
			Voyage once = get_newest_sea_routes();
			once.AddPopup(p);

			// 목록다시그리기リクエスト
			m_req_redraw_list.Request();
		}

		/*-------------------------------------------------------------------------
		 accident を추가する
		---------------------------------------------------------------------------*/
		private void add_accident(SeaRoutePopupPoint p) {
			Voyage once = get_newest_sea_routes();
			once.AddAccident(p);

			// 목록다시그리기リクエスト
			m_req_redraw_list.Request();
		}

		/*-------------------------------------------------------------------------
		 SeaRoutePoint を추가する
		---------------------------------------------------------------------------*/
		private void add_point(SeaRoutePoint p) {
			Voyage once = get_newest_sea_routes();
			once.AddPoint(p);

			// 목록다시그리기リクエスト
			m_req_redraw_list.Request();
		}

		/*-------------------------------------------------------------------------
		 말풍선と항로도の保存
		---------------------------------------------------------------------------*/
		public bool Write(string file_name) {
			return write(m_sea_routes, file_name);
		}

		/*-------------------------------------------------------------------------
		 즐겨찾기항로도の保存
		---------------------------------------------------------------------------*/
		public bool WriteFavorite(string file_name) {
			return write(m_favorite_sea_routes, file_name);
		}

		/*-------------------------------------------------------------------------
		 ごみ箱항로도の保存
		---------------------------------------------------------------------------*/
		public bool WriteTrash(string file_name) {
			return write(m_trash_sea_routes, file_name);
		}

		/*-------------------------------------------------------------------------
		 항로도書き出し
		 sub
		---------------------------------------------------------------------------*/
		static private bool write(List<Voyage> list, string file_name) {
			try {
				using (StreamWriter sw = new StreamWriter(
					file_name, false, Encoding.GetEncoding("UTF-8"))) {

					foreach (Voyage p in list) {
						// 空なら書き出さない
						if (p.IsEmpty) continue;

						sw.WriteLine("start routes");
						p.Write(sw);
					}
				}
			} catch {
				return false;
			}
			return true;
		}

		/*-------------------------------------------------------------------------
		 空きの항로を삭제する
		---------------------------------------------------------------------------*/
		private void remove_empty_routes() {
			while (true) {
				Voyage p = find_empty_routes();
				if (p == null) break;
				m_sea_routes.Remove(p);
			}
		}

		/*-------------------------------------------------------------------------
		 空きの항로を探す
		---------------------------------------------------------------------------*/
		private Voyage find_empty_routes() {
			foreach (Voyage p in m_sea_routes) {
				if (p.IsEmpty) return p;
			}
			return null;
		}

		/*-------------------------------------------------------------------------
		 스크린샷용に바운딩 박스を求める
		 X방향に전부の사이즈が必要なときは
		 태평양で区切られることに注意
		 それ以외は最소の사이즈が得られる
		---------------------------------------------------------------------------*/
		public void CalcScreenShotBoundingBox(out Point offset, out Size size) {
			offset = new Point(0, 0);
			size = new Size((int)m_lib.loop_image.ImageSize.X, (int)m_lib.loop_image.ImageSize.Y);

			// 64ドット단위분布を조사
			int map_count = (int)m_lib.loop_image.ImageSize.X / SS_DISTRIBUTION_X;
			if (((int)m_lib.loop_image.ImageSize.X % SS_DISTRIBUTION_X) != 0) map_count++;
			List<Point>[] map = new List<Point>[map_count];
			for (int i = 0; i < map_count; i++) {
				map[i] = new List<Point>();
			}

			int min_y = (int)m_lib.loop_image.ImageSize.Y;
			int max_y = 0;

			// X방향はおおまかな区切りに등록する
			// Y방향はそのまま최대最소を得る
			check_select_mode();
			IEnumerable<Voyage> list = (m_is_select_mode)
														? enum_all_sea_routes()
														: enum_sea_routes_without_trash();
			foreach (Voyage once in list) {
				once.SS_AddMinMaxList(map, ref min_y, ref max_y, m_is_select_mode);
			}

			// 1つも항로도がない場合はなにもせず帰る
			if (is_empty_list(map)) {
				offset = new Point(0, 0);
				size = new Size(0, 0);
				return;
			}

			// X방향の최대と最소を求める
			// 空きの최대を得る
			int start_index_x, free_count_x;
			calc_bounding_box_x(map, out start_index_x, out free_count_x);
			// 사용범위を求める
			int size_x, offset_x;
			calc_screenshot_range(map, start_index_x, free_count_x, out size_x, out offset_x);

			// オフセットと사이즈설정
			// 지도の가로사이즈よりも대きくなることがある
			size.Width = size_x;
			size.Width = (size.Width + 1) & ~1; // 2の배수とする
			offset.X = offset_x;

			size.Height = max_y - min_y;
			size.Height = (size.Height + 1) & ~1;   // 2の배수とする
			offset.Y = min_y;

			// ギャップを추가する
			offset.X -= SCREEN_SHOT_BOUNDING_BOX_GAP_X;
			offset.Y -= SCREEN_SHOT_BOUNDING_BOX_GAP_Y;
			size.Width += SCREEN_SHOT_BOUNDING_BOX_GAP_X * 2;
			size.Height += SCREEN_SHOT_BOUNDING_BOX_GAP_Y * 2;
		}

		/*-------------------------------------------------------------------------
		 스크린샷용の범위を求める
		---------------------------------------------------------------------------*/
		private void calc_screenshot_range(List<Point>[] map, int start_index_x, int free_count_x, out int size_x, out int offset_x) {
			if (free_count_x <= 0) {
				// 空きがないので全부書き出す
				// 일付변경선あたりで区切る
				offset_x = (int)-(m_lib.loop_image.ImageSize.X / 2);
				size_x = (int)m_lib.loop_image.ImageSize.X;
				return;
			}

			// 空きから사용범위を求める
			int use_count = map.Length - free_count_x;
			int min = start_index_x + free_count_x;
			if (min >= map.Length) min -= map.Length;
			if (min < 0) min += map.Length;
			int max = min + use_count - 1;
			if (max >= map.Length) max -= map.Length;
			if (max < 0) max += map.Length;

			// 최대と最소を得る
			Point min_point = map[min][0];
			foreach (Point p in map[min]) {
				if (p.X < min_point.X) min_point = p;
			}
			Point max_point = map[max][0];
			foreach (Point p in map[max]) {
				if (p.X > max_point.X) max_point = p;
			}

			// 切り出し시작위치
			offset_x = min_point.X;
			// 사이즈
			if (max < min) {
				// 최대のほうが소さい場合は지도사이즈を足してから引く
				size_x = (max_point.X + (int)m_lib.loop_image.ImageSize.X) - min_point.X;
			} else {
				// 普通に최대から最소を引く
				size_x = max_point.X - min_point.X;
			}
		}

		/*-------------------------------------------------------------------------
		 1つも항로도がないかどうか조사
		---------------------------------------------------------------------------*/
		private bool is_empty_list(List<Point>[] map) {
			int map_count = map.Length;
			for (int i = 0; i < map_count; i++) {
				if (map[i].Count > 0) return false; // 항로도あり
			}
			// 항로도없음
			return true;
		}

		/*-------------------------------------------------------------------------
		 最も広い空きを見つける
		---------------------------------------------------------------------------*/
		private void calc_bounding_box_x(List<Point>[] map, out int start_index, out int free_count) {
			int map_count = map.Length;

			int max_start = -1;
			int max_count = 0;
			for (int i = 0; i < map_count; i++) {
				if (map[i].Count <= 0) {
					// 空いてる
					int count = calc_bounding_box_x_sub(map, i);
					if (count > max_count) {
						max_start = i;
						max_count = count;
					}
				}
			}
			start_index = max_start;	// 空き開始인덱스
			free_count = max_count; // 空き수
		}

		/*-------------------------------------------------------------------------
		 連続する空きを조사
		---------------------------------------------------------------------------*/
		private int calc_bounding_box_x_sub(List<Point>[] map, int start) {
			int map_count = map.Length;
			int index = 0;
			int empty_count = 0;
			while (index < map_count) {
				int i = start + index;
				if (i >= map_count) i -= map_count;

				if (map[i].Count > 0) break;		// 空きではない
				empty_count++;
				index++;
			}
			return empty_count;	 // 連続する空きを返す
		}

		/*-------------------------------------------------------------------------
		 선택상태を리셋する
		---------------------------------------------------------------------------*/
		public void ResetSelectFlag() {
			IEnumerable<Voyage> list = enum_all_sea_routes();
			foreach (Voyage p in list) p.IsSelected = false;
		}

		/*-------------------------------------------------------------------------
		 항로목록으로부터 삭제する
		 최신항로도가 삭제된 상황에 대응
		---------------------------------------------------------------------------*/
		public void RemoveSeaRoutes(List<Voyage> remove_list) {
			// 최신の항로도を삭제するかどうかを得る
			if (is_newest_sea_routes(remove_list)) {
				// 최신が消えるので
				// 시작直후の상태にする
				init_add_points();
			}

			// 삭제
			remove_sea_routes(m_sea_routes, remove_list);
		}

		/*-------------------------------------------------------------------------
		 항로목록から삭제する
		---------------------------------------------------------------------------*/
		public void remove_sea_routes(List<Voyage> list, List<Voyage> remove_list) {
			foreach (Voyage i in remove_list) {
				try {
					list.Remove(i);
				} catch {
				}
			}
		}

		/*-------------------------------------------------------------------------
		 항로도즐겨찾기목록から삭제する
		 無条건で삭제して問題ない
		---------------------------------------------------------------------------*/
		public void RemoveFavoriteSeaRoutes(List<Voyage> remove_list) {
			remove_sea_routes(m_favorite_sea_routes, remove_list);
		}

		/*-------------------------------------------------------------------------
		 ごみ箱入り항로목록から삭제する
		 無条건で삭제して問題ない
		---------------------------------------------------------------------------*/
		public void RemoveTrashSeaRoutes(List<Voyage> remove_list) {
			remove_sea_routes(m_trash_sea_routes, remove_list);
		}

		/*-------------------------------------------------------------------------
		 최신の항로도を삭제するかどうかを得る
		---------------------------------------------------------------------------*/
		private bool is_newest_sea_routes(List<Voyage> list) {
			Voyage once = get_newest_sea_routes();
			foreach (Voyage i in list) {
				if (i == once) return true;
			}
			return false;
		}

		/*-------------------------------------------------------------------------
		 항로도の이동
		 항로도から즐겨찾기항로도
		---------------------------------------------------------------------------*/
		public void MoveSeaRoutesToFavoriteSeaRoutes(List<Voyage> move_list) {
			// 추가
			add_sea_routes(m_sea_routes, m_favorite_sea_routes, move_list);
			// 元を삭제
			RemoveSeaRoutes(move_list);
		}

		/*-------------------------------------------------------------------------
		 항로도の이동
		 항로도から과거의항로도
		---------------------------------------------------------------------------*/
		public void MoveSeaRoutesToTrashSeaRoutes(List<Voyage> move_list) {
			// 추가
			add_sea_routes(m_sea_routes, m_trash_sea_routes, move_list);
			// 元を삭제
			RemoveSeaRoutes(move_list);
		}

		/*-------------------------------------------------------------------------
		 항로도の이동
		 즐겨찾기항로도から과거의항로도
		---------------------------------------------------------------------------*/
		public void MoveFavoriteSeaRoutesToTrashSeaRoutes(List<Voyage> move_list) {
			// 추가
			add_sea_routes(m_favorite_sea_routes, m_trash_sea_routes, move_list);
			// 元を삭제
			RemoveFavoriteSeaRoutes(move_list);
		}

		/*-------------------------------------------------------------------------
		 항로도の이동
		 과거의항로도から즐겨찾기항로도
		---------------------------------------------------------------------------*/
		public void MoveTrashSeaRoutesToFavoriteSeaRoutes(List<Voyage> move_list) {
			// 추가
			add_sea_routes(m_trash_sea_routes, m_favorite_sea_routes, move_list);
			// 元を삭제
			RemoveTrashSeaRoutes(move_list);
		}

		/*-------------------------------------------------------------------------
		 항로도の이동
		 이동元にある항로도のみ이동する
		 이동元になければ이동しない
		---------------------------------------------------------------------------*/
		private void add_sea_routes(List<Voyage> from_list, List<Voyage> to_list, List<Voyage> move_list) {
			foreach (Voyage i in move_list) {
				if (has_sea_routes(from_list, i)) {
					// 추가
					to_list.Add(i);
				}
			}
		}

		/*-------------------------------------------------------------------------
		 지정된항로도が목록に含まれるか조사
		---------------------------------------------------------------------------*/
		private bool has_sea_routes(List<Voyage> list, Voyage routes) {
			foreach (Voyage i in list) {
				if (i == routes) return true;
			}
			return false;
		}
	}

	/*-------------------------------------------------------------------------
	 그리기색
	 반투명부분は0を返すため, 사용時に반투명値をorすること
	---------------------------------------------------------------------------*/
	static internal class DrawColor {
		// 그리기색
		static private int[] m_color_tbl = new int[]{
										Color.FromArgb(0, 224,  64,  64).ToArgb(),	// 적
										Color.FromArgb(0, 224, 160,   0).ToArgb(),	// 오렌지
										Color.FromArgb(0, 224, 224,   0).ToArgb(),	// 황
										Color.FromArgb(0,  64, 160,  64).ToArgb(),	// 녹
										Color.FromArgb(0,  30, 160, 160).ToArgb(),	// 푸른
										Color.FromArgb(0, 120, 120, 230).ToArgb(),	// 연보라
										Color.FromArgb(0, 255, 255, 255).ToArgb(),	// 백
										Color.FromArgb(0, 255, 150, 150).ToArgb(),	// 핑크
									};

		/*-------------------------------------------------------------------------
		 그리기색を得る
		---------------------------------------------------------------------------*/
		static public int GetColor(int color_index) {
			if (color_index < 0) return m_color_tbl[0];
			if (color_index >= 8) return m_color_tbl[0];
			return m_color_tbl[color_index];
		}
	}
}
