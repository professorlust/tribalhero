﻿package src.UI.Components
{
	import flash.events.*;
	import org.aswing.*;
	import org.aswing.border.EmptyBorder;
	import org.aswing.event.*;
	import src.*;
	import src.Map.Username;
	import src.UI.Components.*;

	public class CityLabel extends JLabelButton
	{
		public var cityId: int;

		public function CityLabel(cityId: int, cityName: String = null)
		{
			super("-");
			
			setBorder(new EmptyBorder());
			
			setHorizontalAlignment(AsWingConstants.LEFT);
			
			this.cityId = cityId;
			
			if (cityName)
				setText(cityName);
			else
				Global.map.usernames.cities.getUsername(cityId, onReceiveUsername);

			new SimpleTooltip(this, "Go to city");
			
			addEventListener(MouseEvent.MOUSE_DOWN, function(e: MouseEvent) : void {
				goToCity();
			});
		}
		
		public function goToCity() : void {
			Global.gameContainer.clearAllSelections();
			Global.gameContainer.closeAllFrames(true);			
			Global.mapComm.City.gotoCityLocation(cityId);
		}
		
		private function onReceiveUsername(username: Username, custom: *) : void {
			setText(username.name);
			repaintAndRevalidate();
		}
	}

}
