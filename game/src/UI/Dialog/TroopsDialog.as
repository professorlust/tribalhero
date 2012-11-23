﻿package src.UI.Dialog {
	import flash.events.*;	
	import org.aswing.*;
	import org.aswing.border.*;
	import org.aswing.colorchooser.*;
	import org.aswing.event.*;
	import org.aswing.ext.*;
	import org.aswing.geom.*;
	import src.*;
	import src.Map.*;
	import src.Objects.*;
	import src.Objects.Actions.Action;
	import src.Objects.Actions.Notification;
	import src.Objects.Actions.NotificationManager;
	import src.Objects.Process.*;
	import src.Objects.Troop.*;
	import src.UI.*;
	import src.UI.Components.TroopsDialogTable.*;
	import src.Util.*;
	import src.Util.BinaryList.BinaryListEvent;
	import System.Linq.Enumerable;
            
	public class TroopsDialog extends GameJPanel {
		
		private var lstCities:JComboBox;			
		private var btnAttack: JLabelButton;		
		private var btnDefend: JLabelButton;
		
		private var localTroops: VectorListModel;
		private var incomingTroops: VectorListModel;
		private var onTheMoveTroops: VectorListModel;
		private var stationedAwayTroops: VectorListModel;
		
		private var pnlLocalGroup: JPanel;
		private var pnlIncomingGroup: JPanel;
		private var pnlOnTheMoveGroup: JPanel;
		private var pnlStationedAwayGroup: JPanel;
		
		private var lblCitiesUnderAttack: JLabel;
		private var lblIncomingDefense: JLabel;
		private var lblIncomingAttack: JLabel;
		private var lblStationedTroops: JLabel;
		private var lblTroopsOnTheMove: JLabel;
		private var lblTroopsReturningHome: JLabel;
		
		public function TroopsDialog(defaultCity: City)
		{
			createUI();
			title = "Troops";
			
			for each (var city: City in Global.map.cities) {
				(lstCities.getModel() as VectorListModel).append( { id: city.id, city: city, toString: function() : String { return this.city.name; } } );				
				
				city.troops.addEventListener(BinaryListEvent.ADDED, onTroopAdded, false, 0, true);
				city.troops.addEventListener(BinaryListEvent.REMOVED, onTroopRemoved, false, 0, true);
				city.troops.addEventListener(BinaryListEvent.UPDATED, onTroopUpdated, false, 0, true);
			}			
			
			lstCities.setSelectedIndex(0);
			for (var i: int = 0; i < lstCities.getModel().getSize(); i++) {
				var item: * = lstCities.getModel().getElementAt(i);
				
				if (item.hasOwnProperty("id") && item.id == defaultCity.id) {
					lstCities.setSelectedIndex(i);
					break;
				}
			}
			
			btnAttack.addActionListener(onClickAttack);
			btnDefend.addActionListener(onClickReinforce);		
			
			update();
		}
		
		public function show(owner:* = null, modal:Boolean = false, onClose:Function = null):JFrame
		{
			super.showSelf(owner, modal, onClose);

			Global.gameContainer.showFrame(frame);
			
			frame.setResizable(true);
			frame.setMinimumSize(new IntDimension(640, 345));	
			frame.pack();

			return frame;
		}

		private function createUI(): void {
			setPreferredSize(new IntDimension(Math.min(976, Constants.screenW), Util.getMaxGamePanelHeight()));
			setLayout(new BorderLayout());
			
			var pnlBody: JPanel = new JPanel(new SoftBoxLayout(SoftBoxLayout.Y_AXIS));
			var scrollBody: JScrollPane = Util.createTopAlignedScrollPane(pnlBody);
			scrollBody.setConstraints("Center");
			append(scrollBody);
			
			var pnlHeader: JPanel = new JPanel(new BorderLayout(5));
			pnlHeader.setPreferredHeight(70);
			{							
				var pnlCenter: JPanel = new JPanel(new FlowLayout(AsWingConstants.LEFT, 0, 0, false));
				pnlCenter.setConstraints("Center");
				{
                    var pnlGrid: JPanel = new JPanel(new GridLayout(0, 3, 10, 5));
                    {
                        lblCitiesUnderAttack = new JLabel("", null, AsWingConstants.LEFT);
                        lblIncomingDefense = new JLabel("", null, AsWingConstants.LEFT);
                        lblIncomingAttack = new JLabel("", null, AsWingConstants.LEFT);
                        lblStationedTroops = new JLabel("", null, AsWingConstants.LEFT);
                        lblTroopsOnTheMove = new JLabel("", null, AsWingConstants.LEFT);
                        lblTroopsReturningHome = new JLabel("", null, AsWingConstants.LEFT);	
                        
                        pnlGrid.appendAll(lblCitiesUnderAttack, lblIncomingAttack, lblTroopsOnTheMove, lblStationedTroops, lblIncomingDefense, lblTroopsReturningHome);
                    }
                    pnlCenter.append(pnlGrid);
				}
				
				var pnlRight: JPanel = new JPanel(new SoftBoxLayout(SoftBoxLayout.Y_AXIS, 5));
				pnlRight.setConstraints("East");				
				{
					// City dropdown
					lstCities = new JComboBox();			
					lstCities.setModel(new VectorListModel([StringHelper.localize("STR_OVERVIEW")]));
					lstCities.addEventListener(InteractiveEvent.SELECTION_CHANGED, citySelectionChanged);
					lstCities.setPreferredSize(new IntDimension(128, 22));
					
					// Attack/defend buttons
					btnAttack = new JLabelButton(StringHelper.localize("TROOPS_DIALOG_SEND_ATTACK"), null, AsWingConstants.RIGHT);
					btnDefend = new JLabelButton(StringHelper.localize("TROOPS_DIALOG_SEND_DEFENSE"), null, AsWingConstants.RIGHT);					
					
					pnlRight.appendAll(lstCities, btnAttack, btnDefend);
				}			
				
				pnlHeader.appendAll(pnlCenter, pnlRight);				
			}
			
			var localGroup: * = createGroup(StringHelper.localize("TROOPS_DIALOG_LOCAL_GROUP"), [ TroopTable.COLUMN_NAME, TroopTable.COLUMN_LOCATION, TroopTable.COLUMN_STATUS, TroopTable.COLUMNS_UNITS, TroopTable.COLUMN_ACTIONS ]);
			localTroops = localGroup.table.getModel().getList() as VectorListModel;
			pnlLocalGroup = localGroup.group;
			
			var onTheMoveGroup: * = createGroup(StringHelper.localize("TROOPS_DIALOG_ON_THE_MOVE_GROUP"), [ TroopTable.COLUMN_NAME, TroopTable.COLUMN_STATUS, TroopTable.COLUMNS_UNITS, TroopTable.COLUMN_ACTIONS ]);
			onTheMoveTroops = onTheMoveGroup.table.getModel().getList() as VectorListModel;			
			pnlOnTheMoveGroup = onTheMoveGroup.group;
			
			var stationedGroup: * = createGroup(StringHelper.localize("TROOPS_DIALOG_STATIONED_GROUP"), [ TroopTable.COLUMN_NAME, TroopTable.COLUMN_LOCATION, TroopTable.COLUMN_STATUS, TroopTable.COLUMNS_UNITS, TroopTable.COLUMN_ACTIONS ]);
			stationedAwayTroops = stationedGroup.table.getModel().getList() as VectorListModel;						
			pnlStationedAwayGroup = stationedGroup.group;
			
			pnlBody.appendAll(pnlHeader, localGroup.group, onTheMoveGroup.group, stationedGroup.group);			
		}
		
		private function citySelectionChanged(e:InteractiveEvent):void 
		{		
			update();
			
			btnAttack.setVisible(lstCities.getSelectedIndex() > 0);
			btnDefend.setVisible(lstCities.getSelectedIndex() > 0);
		}
		
		private function update():void 
		{
			localTroops.clear();
			onTheMoveTroops.clear();
			//incomingTroops.clear();
			stationedAwayTroops.clear();
            
            // A list of OUR troops used below for some overview calcs
			var troops: Array = [];
			
			for each (var city: City in Global.map.cities) {
				for each (var troop: TroopStub in city.troops) {                    
                    if (troop.cityId == city.id) {
                        troops.push(troop);
                    }
                    
					addTroop(troop);
				}
			}
            
			// Update overview labels			
			lblCitiesUnderAttack.setText(StringHelper.localize("TROOPS_DIALOG_CITIES_UNDER_ATTACK", Enumerable.from(Global.map.cities)
																									  .where(function (city: City): Boolean { return city.inBattle; } )
																									  .count()));
                                                                                                      
            lblTroopsOnTheMove.setText(StringHelper.localize("TROOPS_DIALOG_ON_THE_MOVE", Enumerable.from(troops)
                                                                                            .where(function (troop: TroopStub): Boolean { return troop.state == TroopStub.MOVING; } )
                                                                                            .count()));
                                                                                            
            lblTroopsReturningHome.setText(StringHelper.localize("TROOPS_DIALOG_RETURNING_HOME", Enumerable.from(troops)
                                                                                            .where(function (troop: TroopStub): Boolean { return troop.state == TroopStub.RETURNING_HOME; } )
                                                                                            .count()));
                                                                                            
            var stationedTroops: int = Enumerable.from(troops).where(function (troop: TroopStub): Boolean { return troop.state == TroopStub.STATIONED || troop.state == TroopStub.BATTLE_STATIONED; } ).count();
            var stationedTroopsInBattle: int = Enumerable.from(troops).where(function (troop: TroopStub): Boolean { return troop.state == TroopStub.BATTLE_STATIONED; } ).count();
            lblStationedTroops.setText(StringHelper.localize("TROOPS_DIALOG_STATIONED_TROOPS", stationedTroops, stationedTroopsInBattle));                                
            
			var incomingAttacks: int = Enumerable.from(Global.map.cities)
						     .selectMany(function(city: City):NotificationManager { return city.notifications; } )							
							 .where(function (notification: Notification): Boolean { return Action.actionCategory[notification.type] == Action.CATEGORY_ATTACK; } )
							 .count();							
			lblIncomingAttack.setText(StringHelper.localize("TROOPS_DIALOG_INCOMING_ATTACK", incomingAttacks));
            
            
			var incomingDefenses: int = Enumerable.from(Global.map.cities)
						     .selectMany(function(city: City):NotificationManager { return city.notifications; } )							
							 .where(function (notification: Notification): Boolean { return Action.actionCategory[notification.type] == Action.CATEGORY_DEFENSE; } )
							 .count();							
			lblIncomingDefense.setText(StringHelper.localize("TROOPS_DIALOG_INCOMING_DEFENSE", incomingDefenses));            
                        		
			// Hide empty groups
			setGroupVisibility();
		}
		
		private function setGroupVisibility():void 
		{
			//pnlIncomingGroup.setVisible(incomingTroops.size() > 0);
			pnlLocalGroup.setVisible(localTroops.size() > 0);
			pnlOnTheMoveGroup.setVisible(onTheMoveTroops.size() > 0);
			pnlStationedAwayGroup.setVisible(stationedAwayTroops.size() > 0);
		}
		
		private function createGroup(title: String, columns: Array): * {
			var troopTable: TroopTable = new TroopTable(columns);
			
			var pnlGroup: JPanel = new JPanel(new SoftBoxLayout(SoftBoxLayout.Y_AXIS, 5));
			pnlGroup.setBorder(new EmptyBorder(null, new Insets(10, 0, 0, 0)));
			
			var lblGroupTitle: JLabel = new JLabel(title, new AssetIcon(new ICON_COLLAPSE), AsWingConstants.LEFT);
			lblGroupTitle.useHandCursor = true;
			lblGroupTitle.addEventListener(MouseEvent.CLICK, function (e: Event): void {				
				if (troopTable.isVisible()) {
					troopTable.setVisible(false);
					lblGroupTitle.setIcon(new AssetIcon(new ICON_EXPAND));
				}
				else {
					troopTable.setVisible(true);
					lblGroupTitle.setIcon(new AssetIcon(new ICON_COLLAPSE));
				}				
			});
			
			pnlGroup.appendAll(lblGroupTitle, troopTable);
			
			return {
				group: pnlGroup,
				table: troopTable
			}
		}
		
		private function addTroop(troop: TroopStub) : void {	
			var selectedCity: City = getSelectedCity();
			
			// Make sure it matches the selected city first
			if (selectedCity && troop.cityId != selectedCity.id) {
				if (!troop.isStationed() || troop.stationedLocation.type != Location.CITY || troop.stationedLocation.cityId != selectedCity.id) {
					return;			
				}
			}
		
			switch (troop.state) {
				case TroopStub.WAITING_IN_DEFENSIVE_ASSIGNMENT:
				case TroopStub.WAITING_IN_OFFENSIVE_ASSIGNMENT:
				case TroopStub.MOVING:
				case TroopStub.RETURNING_HOME:
				case TroopStub.BATTLE:
					onTheMoveTroops.append(troop);
					break;
				case TroopStub.STATIONED:
				case TroopStub.BATTLE_STATIONED:
					if (Global.map.cities.get(troop.cityId)) {
						stationedAwayTroops.append(troop);
					}
					else {
						localTroops.append(troop);
					}					
					break;				
				default:
					localTroops.append(troop);
			}			
			
			localTroops.sortOn(["id", "cityId"], Array.NUMERIC);
			stationedAwayTroops.sortOn(["id", "cityId"], Array.NUMERIC);
			onTheMoveTroops.sortOn(["id", "cityId"], Array.NUMERIC);
		}		
		
		private function getSelectedCity(): City {
			if (lstCities.getSelectedIndex() <= 0) {
				return null;
			}
			
			return lstCities.getSelectedItem().city;
		}
		
		public function onClickAttack(event: AWEvent):void
		{
			var attackProcess: AttackSendProcess = new AttackSendProcess(getSelectedCity());
			attackProcess.execute();
		}

		public function onClickReinforce(event: AWEvent):void
		{		
			var reinforcementProcess: ReinforcementSendProcess = new ReinforcementSendProcess(getSelectedCity());
			reinforcementProcess.execute();
		}
		
		private function onTroopUpdated(e: BinaryListEvent) : void {
			update();
		}

		private function onTroopRemoved(e: BinaryListEvent) : void {
			update();
		}

		private function onTroopAdded(e: BinaryListEvent) : void {
			update();
		}		
		
		private function removeStubFromVectorList(list: VectorListModel, troop: TroopStub): void {			
			for (var i: int = 0; i < list.getSize(); i++) {
				var item: * = list.getElementAt(i);
				
				if (TroopStub.compareCityIdAndTroopId(item, [troop.cityId, troop.id]) == 0) {
					list.removeAt(i);
					break;
				}
			}				
		}
		
	}

}

