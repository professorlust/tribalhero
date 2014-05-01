package src.UI.Dialog {
    import com.codecatalyst.promise.Deferred;
    import com.codecatalyst.promise.Promise;

    import org.aswing.AsWingConstants;
    import org.aswing.BorderLayout;
    import org.aswing.GridLayout;
    import org.aswing.Insets;
    import org.aswing.JButton;
    import org.aswing.JFrame;
    import org.aswing.JLabel;
    import org.aswing.JPanel;
    import org.aswing.SoftBoxLayout;
    import org.aswing.UIManager;
    import org.aswing.border.EmptyBorder;
    import org.aswing.ext.Form;
    import org.aswing.ext.MultilineLabel;

    import src.Constants;
    import src.Global;
    import src.UI.Components.CoinLabel;
    import src.UI.GameJPanel;
    import src.UI.LookAndFeel.GameLookAndFeel;
    import src.UI.LookAndFeel.GamePanelBackgroundDecorator;
    import src.UI.ViewModels.StoreBuyCoinsVM;
    import src.Util.StringHelper;

    public class StoreBuyCoinsDialog extends GameJPanel {
        private var viewModel: StoreBuyCoinsVM;
        private var purchaseThemeDeferred: Deferred;

        public function StoreBuyCoinsDialog(viewModel: StoreBuyCoinsVM) {
            this.viewModel = viewModel;
            this.purchaseThemeDeferred = new Deferred();

            createUI();
        }

        private function createUI(): void {
            setPreferredWidth(500);
            setLayout(new SoftBoxLayout(SoftBoxLayout.Y_AXIS, 10));

            append(new MultilineLabel(StringHelper.localize("STORE_BUY_COINS_DIALOG_DESCRIPTION"), 0, 50));

            var formBalance: Form = new Form();
            formBalance.addRow(new JLabel(StringHelper.localize("STORE_BUY_COINS_DIALOG_BALANCE"), null, AsWingConstants.LEFT),
                               new CoinLabel(Constants.coins));

            if (viewModel.itemCost > 0) {
                formBalance.addRow(new JLabel(StringHelper.localize("STORE_BUY_COINS_DIALOG_PURCHASE_COST"), null, AsWingConstants.LEFT),
                        new CoinLabel(viewModel.itemCost));
            }

            if (Constants.coins < viewModel.itemCost) {
                formBalance.addRow(new JLabel(StringHelper.localize("STORE_BUY_COINS_DIALOG_NEEDED"), null, AsWingConstants.LEFT),
                        new CoinLabel(viewModel.itemCost - Constants.coins));
            }

            append(formBalance);

            var pricesPanel: JPanel = new JPanel(new GridLayout(1, 3, 20));
            pricesPanel.append(createRefillItem("REFILL5", 5, 200, 0));
            pricesPanel.append(createRefillItem("REFILL10", 10, 450, 11));
            pricesPanel.append(createRefillItem("REFILL15", 15, 700, 16));
            pricesPanel.append(createRefillItem("REFILL20", 20, 1000, 20));

            append(pricesPanel);
        }

        private function createRefillItem(refillPackage: String, cost: int, coins: int, discount: int): JPanel
        {
            var wrapper: JPanel = new JPanel(new SoftBoxLayout(SoftBoxLayout.Y_AXIS, 5));

            var lblCoins: CoinLabel = new CoinLabel(coins);
            lblCoins.setHorizontalAlignment(AsWingConstants.CENTER);
            GameLookAndFeel.changeClass(lblCoins, "darkSectionHeader");

            var btnBuy: JButton = new JButton(StringHelper.localize("STORE_BUY_COINS_DIALOG_REFILL_PRICE", cost));

            wrapper.appendAll(lblCoins, btnBuy);

            if (discount > 0) {
                var lblDiscount: JLabel = new JLabel(StringHelper.localize("STORE_BUY_COINS_DIALOG_REFILL_DISCOUNT", discount));
                wrapper.append(lblDiscount);
            }

            wrapper.setBackgroundDecorator(new GamePanelBackgroundDecorator("TabbedPane.top.contentRoundImage"));
            wrapper.setBorder(new EmptyBorder(null, UIManager.get("TabbedPane.contentMargin") as Insets));

            var localRefRefillPackage: String = refillPackage;
            btnBuy.addActionListener(function(): void {
                viewModel.buy(localRefRefillPackage);
            });

            return wrapper;
        }

        public function show(owner:* = null, modal:Boolean = true, onClose:Function = null):JFrame
        {
            super.showSelf(owner, modal, onClose, null);

            frame.setResizable(false);
            frame.pack();

            Global.gameContainer.showFrame(frame);

            return frame;
        }

        public function get purchaseThemePromise(): Promise {
            return purchaseThemeDeferred.promise;
        }
    }
}