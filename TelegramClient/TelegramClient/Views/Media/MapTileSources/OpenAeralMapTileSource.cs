﻿using System;
using Microsoft.Phone.Controls.Maps;

namespace TelegramClient.Views.Media.MapTileSources
{
    public class OpenAeralMapTileSource : TileSource
    {
        public OpenAeralMapTileSource()
	    {
	        //Uri base of an OpenSTreetMap server
            UriFormat = "http://tile.openaerialmap.org/tiles/1.0.0/openaerialmap-900913/{2}/{0}/{1}.jpg";
	    }
 
	    public override Uri GetUri(int x, int y, int zoomLevel)
        { 
            if (zoomLevel > 0 && zoomLevel <= 18)
            {
                var url = string.Format(UriFormat,
                    zoomLevel,
                    x,
                    y);
 
                return new Uri(url);
            }
			//if zoom level is not supported, return null
            return null;
        }
    }
}
