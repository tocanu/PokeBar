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
        MoneyRun.Text = $"{_state.Money}";
        
        // Contar todas as Pokébolas
        int totalBalls = 0;
        foreach (BallType ballType in Enum.GetValues(typeof(BallType)))
        {
            totalBalls += _state.Inventory.GetValueOrDefault(ballType, 0);
        }
        
        InventoryText.Text = $"📦 Pokébolas no inventário: {totalBalls}";
    }

    private void CreateShopItems()
    {
        ItemsPanel.Children.Clear();

        // Obter apenas as pokébolas compráveis do BallDefinition
        var purchasableBalls = BallDefinition.GetPurchasable();

        foreach (var ball in purchasableBalls)
        {
            AddShopItem(ball);
        }
    }

    private void AddShopItem(BallDefinition ball)
    {
        // Card estilo GBA
        var card = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#70C5EC")),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28546E")),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 5)
        };

        card.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            Opacity = 0.2,
            BlurRadius = 0,
            ShadowDepth = 2,
            Direction = 315
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Ícone
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Info
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Botão

        // Ícone da Pokébola
        var iconText = new TextBlock
        {
            Text = ball.Icon,
            FontSize = 32,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(iconText, 0);
        grid.Children.Add(iconText);

        // Info do item
        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameText = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 2)
        };
        nameText.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            Opacity = 1,
            BlurRadius = 0,
            ShadowDepth = 1,
            Direction = 315
        };
        nameText.Inlines.Add(new System.Windows.Documents.Run(ball.Name));
        infoStack.Children.Add(nameText);

        var statsText = new TextBlock
        {
            FontSize = 11,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 3)
        };
        statsText.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            Opacity = 0.8,
            BlurRadius = 0,
            ShadowDepth = 1,
            Direction = 315
        };
        statsText.Inlines.Add(new System.Windows.Documents.Run($"Taxa de captura: {ball.CatchRateMultiplier}x"));
        infoStack.Children.Add(statsText);

        var priceText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700"))
        };
        priceText.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            Opacity = 1,
            BlurRadius = 0,
            ShadowDepth = 1,
            Direction = 315
        };
        priceText.Inlines.Add(new System.Windows.Documents.Run($"¥{ball.Price}"));
        infoStack.Children.Add(priceText);

        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        // Botão Comprar estilo GBA
        var buyButton = new Button
        {
            Content = "💰 COMPRAR",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Tag = ball // Store the ball definition in Tag
        };
        buyButton.SetResourceReference(Button.StyleProperty, "BuyButton");
        buyButton.Click += (s, e) => BuyItem(ball.Type, ball.Price);

        Grid.SetColumn(buyButton, 2);
        grid.Children.Add(buyButton);

        card.Child = grid;
        ItemsPanel.Children.Add(card);
    }

    private void BuyItem(BallType ballType, int price)
    {
        if (_state.Money >= price)
        {
            _state.Money -= price;
            _state.Inventory[ballType] = _state.Inventory.GetValueOrDefault(ballType, 0) + 1;
            _onPurchase();
            UpdateUI();

            // Feedback sonoro seria ideal aqui (System.Media.SystemSounds.Beep.Play())
        }
        else
        {
            MessageBox.Show(
                "Você não tem dinheiro suficiente!",
                "⚠️ Fundos Insuficientes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
