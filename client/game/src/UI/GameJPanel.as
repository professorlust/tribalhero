﻿package src.UI
{
    import flash.utils.getDefinitionByName;
    import flash.utils.getQualifiedClassName;

    import org.aswing.Border;
    import org.aswing.Insets;
    import org.aswing.JFrame;
    import org.aswing.JPanel;
    import org.aswing.border.EmptyBorder;

    import src.Global;

    import src.Util.Util;

    public class GameJPanel extends JPanel
	{
        protected var allowMultipleInstances: Boolean = true;
		protected var frame: GameJFrame = null;
		protected var title: String = "";
		private var originalBorder: Border;

		public function GameJPanel()
		{
		}

		public function getFrame(): GameJFrame {
			return frame;
		}

		public function showSelf(owner: * = null, modal: Boolean = true, onClose: Function = null, onDispose: Function = null) : JFrame {
			if (!allowMultipleInstances) {
                var type: Class = Class(getDefinitionByName(getQualifiedClassName(this)));
                var existingDialog: GameJPanel = Global.gameContainer.findDialog(type);
                if (existingDialog) {
                    return existingDialog.getFrame();
                }
            }

			if (!originalBorder) {
				originalBorder = getBorder();
			}

			var self: JPanel = this;
			frame = new GameJFrame(owner, title, modal, function(): void {
				if (onClose != null) 
					onClose(self);
				
				if (onDispose != null)
					onDispose();
			});
			setBorder(new EmptyBorder(originalBorder, new Insets(15, 22, 30, 25)));

			frame.setContentPane(this);
			
			frame.pack();
			Util.centerFrame(frame);
			frame.setResizable(false);
			return frame;
		}
	}

}

