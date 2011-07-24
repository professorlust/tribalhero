﻿package src.UI.Components.BattleReport
{
	import flash.events.Event;
	import org.aswing.event.SelectionEvent;
	import org.aswing.event.TableCellEditEvent;
	import org.aswing.table.GeneralTableCellFactory;
	import org.aswing.table.PropertyTableModel;
	import src.Comm.GameURLLoader;
	import src.Constants;
	import src.Global;
	import src.UI.Components.SimpleTooltip;
	import src.UI.GameJPanel;
	import org.aswing.*;
	import org.aswing.border.*;
	import org.aswing.geom.*;
	import org.aswing.colorchooser.*;
	import org.aswing.ext.*;
	import flash.net.*;
	import src.UI.Dialog.*;
	/**
	 * ...
	 * @author Giuliano Barberi
	 */
	public class RemoteReportList extends GameJPanel
	{

		private var tblReports:JTable;
		private var pnlPaging:JPanel;
		private var btnPrevious:JLabelButton;
		private var lblPages:JLabel;
		private var btnNext:JLabelButton;
		private var reportList: VectorListModel;
		private var tableModel: PropertyTableModel;		
		
		private var loader: GameURLLoader = new GameURLLoader();
		private var page: int = 0;
		private var playerNameFilter: String = "";
		
		public var refreshOnClose: Boolean = false;
		
		public function RemoteReportList()
		{
			createUI();
			loader.addEventListener(Event.COMPLETE, onLoaded);
			
			tblReports.addEventListener(TableCellEditEvent.EDITING_STARTED, function(e: TableCellEditEvent) : void {
				tblReports.getCellEditor().stopCellEditing();
			});

			tblReports.addEventListener(SelectionEvent.ROW_SELECTION_CHANGED, function(e: SelectionEvent) : void {
				if (tblReports.getSelectedRow() == -1) return;

				var row: * = reportList.get(tblReports.getSelectedRow());
				var id: int = row.id;

				tblReports.clearSelection(true);

				var battleReportDialog: BattleReportViewer = new BattleReportViewer(id, playerNameFilter, false);
				battleReportDialog.show(null, true, function(viewDialog: BattleReportViewer = null) : void {
					if (battleReportDialog.refreshOnClose) {
						refreshOnClose = true;
						loadPage(page);
					}
				});
			});

			btnNext.addActionListener(function() : void {
				loadPage(page + 1);
			});

			btnPrevious.addActionListener(function() : void{
				loadPage(page - 1);
			});

			loadPage(0);
		}

		public function filterPlayerName(playerName: String) : void {
			playerNameFilter = playerName;
			loadPage(0);
		}		
		
		private function loadPage(page: int) : void {
			btnPrevious.setVisible(false);
			btnNext.setVisible(false);
			lblPages.setText("Loading...");
			lblPages.pack();

			Global.mapComm.BattleReport.listRemote(loader, page, playerNameFilter);
		}

		private function onLoaded(e: Event) : void {
			var data: Object;
			try
			{
				data = loader.getDataAsObject();
			}
			catch (e: Error) {
				InfoDialog.showMessageDialog("Error", "Unable to query report. Refresh the page if this problem persists");
				return;
			}

			//Paging info
			this.page = data.page;
			btnPrevious.setVisible(page > 1);
			btnNext.setVisible(page < data.pages);
			lblPages.setText(data.page + " of " + data.pages);

			tblReports.clearSelection(true);
			reportList.clear();

			//Snapshots
			for each (var snapshot: Object in data.snapshots)
			reportList.append(snapshot);
		}

		private function createUI() : void {
			var layout0:BorderLayout = new BorderLayout();
			setLayout(layout0);

			reportList = new VectorListModel();

			tableModel = new PropertyTableModel(reportList,
			["Date", "Battle Location", "Troop", "Side"],
			["date", ".", "troop", "side"],
			[null, null, null, null]
			);

			tblReports = new JTable(tableModel);
			tblReports.setSelectionMode(JTable.SINGLE_SELECTION);
			tblReports.getColumnAt(1).setCellFactory(new GeneralTableCellFactory(UnreadTextCell));

			var pnlReportsScroll: JScrollPane = new JScrollPane(tblReports);
			pnlReportsScroll.setConstraints("Center");

			pnlPaging = new JPanel();
			pnlPaging.setConstraints("South");

			btnPrevious = new JLabelButton("< Newer");

			lblPages = new JLabel();

			btnNext = new JLabelButton("Older >");					
			
			//component layoution
			append(pnlReportsScroll);
			append(pnlPaging);		

			pnlPaging.append(btnPrevious);
			pnlPaging.append(lblPages);
			pnlPaging.append(btnNext);
		}

	}

}
