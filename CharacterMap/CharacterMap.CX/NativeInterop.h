﻿#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "ColorTextAnalyzer.h"
#include "CanvasTextLayoutAnalysis.h"
#include "DWriteFontSource.h"
#include "DWriteProperties.h"
#include "DWriteFontFace.h"
#include "DWriteFontSet.h"
#include "DWriteFontAxis.h"
#include "DWriteFontAxisAttribute.h"
#include "PathData.h"
#include "GlyphImageFormat.h"
#include "DWriteFallbackFont.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::Graphics::Canvas::Geometry;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Storage::Streams;
using namespace Windows::Storage;
using namespace CharacterMapCX;

namespace CharacterMapCX
{
	ref class NativeInterop;
	public delegate void SystemFontSetInvalidated(NativeInterop^ sender, Platform::Object^ args);

    public ref class NativeInterop sealed
    {
    public:
		event SystemFontSetInvalidated^ FontSetInvalidated;

        NativeInterop(CanvasDevice^ device);

		CanvasTextLayoutAnalysis^ AnalyzeCharacterLayout(CanvasTextLayout^ layout);

		IVectorView<PathData^>^ GetPathDatas(CanvasFontFace^ fontFace, const Platform::Array<UINT16>^ glyphIndicies);

		Platform::String^ GetPathData(CanvasFontFace^ fontFace, UINT16 glyphIndicie);

		/// <summary>
		/// Returns an SVG-Path syntax compatible representation of the Canvas Text Geometry.
		/// </summary>
		PathData^ GetPathData(CanvasGeometry^ geometry);

		DWriteFontSet^ GetSystemFonts();

		DWriteFallbackFont^ CreateEmptyFallback();

		__inline DWriteFontSet^ GetFonts(StorageFile^ files);

		IVectorView<DWriteFontSet^>^ GetFonts(IVectorView<StorageFile^>^ files);

	private:

		DWriteFontSet^ Parse(ComPtr<IDWriteFontCollection3> fontCollection);

		IAsyncAction^ ListenForFontSetExpirationAsync();

		bool m_isFontSetStale = true;
		ComPtr<IDWriteFontSet3> m_systemFontSet;
		DWriteFontSet^ m_appFontSet;

		ComPtr<IDWriteFactory7> m_dwriteFactory;
		ComPtr<ID2D1Factory5> m_d2dFactory;
		ComPtr<ID2D1DeviceContext1> m_d2dContext;

		CustomFontManager* m_fontManager;
    };
}
