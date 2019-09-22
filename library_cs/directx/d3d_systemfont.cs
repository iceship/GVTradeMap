﻿/*-------------------------------------------------------------------------

 Direct3D
 시스템フォント
 D3DXFontよりも고속だが, ASCII문자のみの그리기となる
 MS UI Gothicを少しだけ改造したフォント

---------------------------------------------------------------------------*/

/*-------------------------------------------------------------------------
 using
---------------------------------------------------------------------------*/
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

using System.IO;
using System.Reflection;
using System;

/*-------------------------------------------------------------------------

---------------------------------------------------------------------------*/
namespace directx
{
	/*-------------------------------------------------------------------------

	---------------------------------------------------------------------------*/
	public class d3d_systemfont : IDisposable
	{
		private const int					HEIGHT		= 12;
		private const int					DEF_WIDTH	= 5;
	
		/*-------------------------------------------------------------------------

		---------------------------------------------------------------------------*/
		private d3d_device					m_d3d_device;		// 
		private d3d_sprite_rects			m_sprite_rects;		// 관리용스프라이트
		private d3d_sprite					m_sprite;			// 그리기관리

		private int[]						m_width_tbl;		// 幅테이블

		private Vector3						m_position;			// Puts()時の표시위치
		private float						m_return_x;			// 개행時のX위치

		/*-------------------------------------------------------------------------

		---------------------------------------------------------------------------*/
		public Vector3 locate			{	get{	return m_position;				}
											set{	m_position	= value;
													m_return_x	= m_position.X;		}}
		public Texture texture			{	get{	return m_sprite_rects.texture;	}}
		public int sprite_count			{	get{	return m_sprite.draw_sprites_in_frame;	}}
	
		/*-------------------------------------------------------------------------
		 초기화
		---------------------------------------------------------------------------*/
		public d3d_systemfont(d3d_device device)
		{
			if(device == null)	return;

			// 2のべき乗でない場合強制的に2のべき乗に변환されてしまうので注意
			// 2のべき乗で絵を作ること
			Assembly	ass		= Assembly.GetExecutingAssembly();
			Stream		stream	= ass.GetManifestResourceStream("directx.image.systemfont_8x16_ui_gothic.dds");
			m_sprite_rects	= new d3d_sprite_rects(device, stream);
			initialize(device);
		}
	
		/*-------------------------------------------------------------------------
		 
		---------------------------------------------------------------------------*/
		public void Dispose()
		{
			if(m_sprite_rects != null)		m_sprite_rects.Dispose();
			if(m_sprite != null)			m_sprite.Dispose();
			m_sprite_rects	= null;
			m_sprite		= null;
		}
	
		/*-------------------------------------------------------------------------
		 초기화
		---------------------------------------------------------------------------*/
		private void initialize(d3d_device device)
		{
			m_d3d_device	= device;
			if(device.device == null)	return;

			m_sprite		= new d3d_sprite(device.device, device.is_use_ve1_1_ps1_1);

			// 矩形등록
			init_rects();

			// 初期위치
			locate			= new Vector3(0, 0, 0.1f);
		}

		/*-------------------------------------------------------------------------
		 矩形등록
		---------------------------------------------------------------------------*/
		private void init_rects()
		{
			Vector2		offset	= new Vector2(0, 0);
			for(int i=0; i<6; i++){
				for(int j=0; j<16; j++){
					m_sprite_rects.AddRect(	offset, 
										new Rectangle(j*8, i*16, 8, 12));
				}
			}
			
			// 幅테이블
			m_width_tbl			= new int[]{
				//   ! " # $ % & ' ( ) * + , - . / 
				   6,4,6,7,6,7,7,4,4,4,6,6,3,6,3,7,
				// 0 1 2 3 4 5 6 7 8 9 : ; < = > ? 
				   6,6,6,6,6,6,6,6,6,6,4,4,6,6,6,6,
				// @ A B C D E F G H I J K L M N O 
				   8,7,7,8,7,6,6,8,7,4,6,6,5,8,7,8,
				// P Q R S T U V W X Y Z [ \ ] ^ _ 
				   6,8,8,7,8,7,9,8,8,8,7,4,6,4,6,5,
				// ` a b c d e f g h i j k l m n o 
				   4,6,6,6,6,6,4,6,6,2,3,6,2,8,6,6,
				// p q r s t u v w x y z { | } ~   
				   6,6,4,6,4,6,6,8,6,6,5,4,2,4,6,6,
			};
		}

		/*-------------------------------------------------------------------------
		 그리기フレームの開始
		 그리기した스프라이트수を초기화する
		---------------------------------------------------------------------------*/
		public void BeginFrame()
		{
			m_sprite.BeginFrame();
		}
	
		/*-------------------------------------------------------------------------
		 그리기時の사이즈を得る
		 リターンコードは無視されるため, 세로は常に HEIGHT を返す
		---------------------------------------------------------------------------*/
		public Rectangle MeasureText(string text)
		{
			Rectangle	rect	= new Rectangle();
			rect.X		= 0;
			rect.Y		= 0;
			rect.Height	= HEIGHT;
			rect.Width	= 0;

			foreach(char a in text){
				int		ch	= (int)a;
				ch	-= 0x20;
				if((ch > 0)&&(ch <= 16*6)){
					rect.Width	+= m_width_tbl[ch];
				}else{
					rect.Width	+= DEF_WIDTH;
				}
			}
			return rect;
		}

		/*-------------------------------------------------------------------------
		 開始と종료
		 Begin();
		 DrawText();
		  :
		 End();
		 と呼ぶこと
		---------------------------------------------------------------------------*/
		public void Begin()
		{
			m_sprite.BeginDrawSprites(m_sprite_rects.texture);
		}
		public void End()
		{
			m_sprite.EndDrawSprites();
		}

		/*-------------------------------------------------------------------------
		 그리기
		 위치지정で개행無視
		---------------------------------------------------------------------------*/
		public void DrawTextR(string text, int x, int y, Color color)
		{
			DrawTextR(text, x, y, 0, color);
		}
		public void DrawTextC(string text, int x, int y, Color color)
		{
			DrawTextC(text, x, y, 0, color);
		}
		public void DrawTextR(string text, int x, int y, float z, Color color)
		{
			Rectangle rect	= MeasureText(text);
			DrawText(text, x - rect.Width, y, z, color);
		}
		public void DrawTextC(string text, int x, int y, float z, Color color)
		{
			Rectangle rect	= MeasureText(text);
			DrawText(text, x - (rect.Width / 2), y, z, color);
		}
		public void DrawText(string text, int x, int y, Color color)
		{
			DrawText(text, x, y, 0, color);
		}
		public void DrawText(string text, int x, int y, float z, Color color)
		{
			Vector3	pos	= new Vector3(x, y, z);
			int		c	= color.ToArgb();

			foreach(char a in text){
				int		ch	= (int)a;
				ch	-= 0x20;
				if((ch > 0)&&(ch <= 16*6)){
					m_sprite.AddDrawSpritesNC(pos, m_sprite_rects.rects[ch], c);
					pos.X	+= m_width_tbl[ch];
				}else{
					pos.X	+= DEF_WIDTH;
				}
			}
		}

		/*-------------------------------------------------------------------------
		 그리기
		 Puts()
		 개행コードが考慮される
		 デバッグ용

		 Begin();
		 End();
		 で囲む必要はない
		---------------------------------------------------------------------------*/
		public void Puts(string text, Color color)
		{
			int		c	= color.ToArgb();
			m_sprite.BeginDrawSprites(m_sprite_rects.texture);
			foreach(char a in text){
				int		ch	= (int)a;
				if(a == '\n'){
					// 개행
					m_position.Y	+= HEIGHT;
					m_position.X	= m_return_x;
				}else if((ch > 0x20)&&(ch <= 0x20+(16*6))){
					ch				-= 0x20;
					m_sprite.AddDrawSpritesNC(m_position, m_sprite_rects.rects[ch], c);
					m_position.X	+= m_width_tbl[ch];
				}else{
					m_position.X	+= DEF_WIDTH;
				}
			}
			m_sprite.EndDrawSprites();
		}
	}
}
