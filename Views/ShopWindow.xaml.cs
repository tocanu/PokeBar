using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PokeBar.Models;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using MessageBox = System.Windows.MessageBox;

namespace PokeBar.Views;

public partial class ShopWindow : Window
{
    private readonly GameState _state;
    private readonly Action _onPurchase;

    public ShopWindow(GameState state, Action onPurchase)
    {
        InitializeComponent();
        _state = state;
        _onPurchase = onPurchase;
        
        UpdateUI();
        CreateShopItems();
    }

    private void UpdateUI()
    {
        MoneyText.Text = $"${_state.Money}";
        
        // Contar todas as Pokébolas
        int totalBalls = 0;
        foreach (BallType ballType in Enum.GetValues(typeof(BallType)))
        {
            totalBalls += _state.Inventory.GetValueOrDefault(ballType.ToString(), 0);
        }
        
        InventoryText.Text = $"📦 Pokébolas: {totalBalls}";
    }

    private void CreateShopItems()
    {
        ItemsPanel.Children.Clear();

        // Pokébolas disponíveis para compra
        AddShopItem("⚪ Poké Ball", "Taxa de captura básica (1x)", 200, BallType.PokeBall, "#EF4444");
        AddShopItem("🔵 Great Ball", "Taxa de captura melhorada (1.5x)", 600, BallType.GreatBall, "#3B82F6");
        AddShopItem("⚫ Ultra Ball", "Alta taxa de captura (2x)", 1200, BallType.UltraBall, "#FBBF24");
        AddShopItem("🌊 Net Ball", "Muito efetiva contra Água/Inseto (3x)", 1000, BallType.NetBall, "#06B6D4");
        AddShopItem("🏊 Dive Ball", "Muito efetiva debaixo d'água (3.5x)", 1000, BallType.DiveBall, "#0EA5E9");
        AddShopItem("🌱 Nest Ball", "Melhor contra Pokémon de baixo nível", 1000, BallType.NestBall, "#84CC16");
        AddShopItem("🔁 Repeat Ball", "Muito efetiva se já capturou (3x)", 1000, BallType.RepeatBall, "#F59E0B");
        AddShopItem("⏱️ Timer Ball", "Aumenta efetividade com turnos", 1000, BallType.TimerBall, "#6B7280");
        AddShopItem("💎 Luxury Ball", "Taxa básica mas aumenta amizade", 1000, BallType.LuxuryBall, "#DC2626");
        AddShopItem("🎁 Premier Ball", "Igual à Poké Ball mas premium (1x)", 200, BallType.PremierBall, "#F87171");
        AddShopItem("🌙 Dusk Ball", "Muito efetiva à noite/cavernas (3x)", 1000, BallType.DuskBall, "#4338CA");
        AddShopItem("❤️ Heal Ball", "Taxa básica mas cura o Pokémon", 300, BallType.HealBall, "#EC4899");
        AddShopItem("⚡ Quick Ball", "Extremamente efetiva no 1º turno (5x)", 1000, BallType.QuickBall, "#EAB308");
        AddShopItem("💨 Fast Ball", "Muito efetiva contra rápidos (4x)", 300, BallType.FastBall, "#F97316");
        AddShopItem("📊 Level Ball", "Efetividade baseada em nível", 300, BallType.LevelBall, "#14B8A6");
        AddShopItem("🎣 Lure Ball", "Muito efetiva contra pescados (4x)", 300, BallType.LureBall, "#3B82F6");
        AddShopItem("⚖️ Heavy Ball", "Efetividade baseada em peso", 300, BallType.HeavyBall, "#71717A");
        AddShopItem("💕 Love Ball", "Extremamente efetiva mesmo gênero (8x)", 300, BallType.LoveBall, "#F472B6");
        AddShopItem("😊 Friend Ball", "Taxa básica mas mais amizade", 300, BallType.FriendBall, "#34D399");
        AddShopItem("🌕 Moon Ball", "Muito efetiva c/ Pedra Lunar (4x)", 300, BallType.MoonBall, "#8B5CF6");
        AddShopItem("🏆 Sport Ball", "Taxa melhorada para competições (1.5x)", 300, BallType.SportBall, "#F59E0B");
    }

    private void AddShopItem(string name, string description, int price, BallType ballType, string accentColor)
    {
        var itemKey = ballType.ToString();
        
        var card = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 16)
        };

        card.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 10,
            ShadowDepth = 0,
            Opacity = 0.1,
            Color = Colors.Black
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftStack = new StackPanel();
        
        var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
        var titleText = new TextBlock
        {
            Text = name,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937"))
        };
        titlePanel.Children.Add(titleText);

        leftStack.Children.Add(titlePanel);

        var descText = new TextBlock
        {
            Text = description,
            FontSize = 13,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
            Margin = new Thickness(0, 4, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };
        leftStack.Children.Add(descText);

        var priceText = new TextBlock
        {
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentColor))
        };
        priceText.Inlines.Add(new System.Windows.Documents.Run($"${price}"));
        leftStack.Children.Add(priceText);

        Grid.SetColumn(leftStack, 0);
        grid.Children.Add(leftStack);

        var buyButton = new Button
        {
            Content = "Comprar",
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentColor)),
            Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24, 10, 24, 10),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            var template = new ControlTemplate(typeof(Button));
            var templateBorder = new FrameworkElementFactory(typeof(Border));
            templateBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            templateBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            templateBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            templateBorder.AppendChild(presenter);
            template.VisualTree = templateBorder;
            buyButton.Template = template;

            buyButton.Click += (s, e) => BuyItem(itemKey, price);
            buyButton.MouseEnter += (s, e) =>
            {
                var color = (Color)ColorConverter.ConvertFromString(accentColor);
                buyButton.Background = new SolidColorBrush(Color.FromRgb(
                    (byte)(color.R * 0.9),
                    (byte)(color.G * 0.9),
                    (byte)(color.B * 0.9)
                ));
            };
            buyButton.MouseLeave += (s, e) =>
            {
                buyButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentColor));
            };

            Grid.SetColumn(buyButton, 1);
            grid.Children.Add(buyButton);

        card.Child = grid;
        ItemsPanel.Children.Add(card);
    }

    private void BuyItem(string itemKey, int price)
    {
        if (_state.Money >= price)
        {
            _state.Money -= price;
            _state.Inventory[itemKey] = _state.Inventory.GetValueOrDefault(itemKey, 0) + 1;
            _onPurchase();
            UpdateUI();

            // Animação de feedback
            ShowPurchaseSuccess();
        }
        else
        {
            MessageBox.Show(
                "Você não tem dinheiro suficiente!",
                "Fundos Insuficientes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void ShowPurchaseSuccess()
    {
        var notification = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 20, 0, 0)
        };

        var text = new TextBlock
        {
            Text = "✓ Item comprado com sucesso!",
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold
        };

        notification.Child = text;
        
        // Adicionar ao grid principal temporariamente
        var mainGrid = (Grid)((Border)Content).Child;
        mainGrid.Children.Add(notification);
        Grid.SetRow(notification, 0);
        Grid.SetRowSpan(notification, 3);

        // Remover após 2 segundos
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        timer.Tick += (s, e) =>
        {
            mainGrid.Children.Remove(notification);
            timer.Stop();
        };
        timer.Start();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
