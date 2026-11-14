using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Kuznecov_41
{
    /// <summary>
    /// Логика взаимодействия для OrderWindow.xaml
    /// </summary>
    public partial class OrderWindow : Window
    {
        List<OrderProduct> selectedOrderProducts = new List<OrderProduct>();
        List<Product> selectedProducts = new List<Product>();
        private Order currentOrder = new Order();
        private OrderProduct currentOrderProduct = new OrderProduct();
        public OrderWindow(List<OrderProduct> selectedOrderProducts, List<Product> selectedProducts, string FIO)
        {
            InitializeComponent();

            // Загружаем пункты выдачи
            var currentPickups = Kuznecov_41Entities1.GetContext().PicpupPoint.ToList();
            PickupCombo.ItemsSource = currentPickups;

            // Заполняем ФИО клиента и номер заказа
            FIOTB.Text = FIO;
            if (selectedOrderProducts.Any())
                OrderNumberTB.Text = selectedOrderProducts.First().OrderID.ToString();

            // Передаем товары в список и проставляем актуальное количество
            ShoeListView.ItemsSource = selectedProducts;
            foreach (Product p in selectedProducts)
            {
                p.Quantity = 1;
                foreach (OrderProduct q in selectedOrderProducts)
                {
                    if (p.ProductArticleNumber == q.ProductArticleNumber)
                        p.Quantity = q.QUantity;
                }
            }

            this.selectedOrderProducts = selectedOrderProducts;
            this.selectedProducts = selectedProducts;
            OrderDP.SelectedDate = DateTime.Now;

            SetDeliveryDate();
            UpdateOrderSummary();
        }

        private void SetDeliveryDate()
        {
            bool allInStockMoreThanThree = selectedProducts.All(p => (p.ProductQuantityInStock - p.Quantity) > 3);

            int daysToAdd = allInStockMoreThanThree ? 3 : 6;
            deliveryDP.SelectedDate = DateTime.Now.AddDays(daysToAdd);
           
        }

        private void UpdateOrderSummary()
        {
            decimal totalSum = 0;
            decimal totalDiscount = 0;

            foreach (var prod in selectedProducts)
            {
                decimal itemPrice = prod.ProductCost * prod.Quantity;
                decimal discount = itemPrice * (Convert.ToDecimal(prod.ProductDiscountAmount) / 100m);

                totalSum += (itemPrice - discount);
                totalDiscount += discount;
            }

            TotalSumTB.Text = $"{totalSum:F2} руб.";
            TotalDiscountTB.Text = $"{totalDiscount:F2} руб.";
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PickupCombo.SelectedItem == null)
            {
                MessageBox.Show("Выберите пункт выдачи!");
                return;
            }

            // 1. Заполняем данные самого заказа
            currentOrder.OrderDate = DateTime.Now;
            currentOrder.OrderDeliveryDate = deliveryDP.SelectedDate.Value;
            currentOrder.OrderPickupPoint = (PickupCombo.SelectedItem as PicpupPoint).PickupPointID; // Проверьте имя ID пункта выдачи!
            currentOrder.OrderStatus = "Новый";
            currentOrder.OrderCode = new Random().Next(100, 999).ToString();

            // Если клиент авторизован — ищем его ID в БД, если Гость — ставим null
            if (FIOTB.Text == "Гость" || string.IsNullOrWhiteSpace(FIOTB.Text))
            {
                currentOrder.OrderClientID = null; // Если в БД поле называется ClientID
            }
            else
            {
                // Ищем пользователя по ФИО
                var user = Kuznecov_41Entities1.GetContext().User.ToList()
                    .FirstOrDefault(u => $"{u.UserSurname} {u.UserName} {u.UserPatronymic}" == FIOTB.Text);
                if (user != null)
                    currentOrder.OrderClientID = user.UserID;
            }

            // 2. Сначала добавляем сам заказ в контекст
            Kuznecov_41Entities1.GetContext().Order.Add(currentOrder);

            // 3. Добавляем товары заказа, привязывая их к объекту currentOrder (БД сама подставит правильный OrderID!)
            foreach (var op in selectedOrderProducts)
            {
                op.OrderID = currentOrder.OrderID;
                Kuznecov_41Entities1.GetContext().OrderProduct.Add(op);
            }

            try
            {
                Kuznecov_41Entities1.GetContext().SaveChanges();
                MessageBox.Show("Заказ успешно сохранен!");
                this.Close();

                var selectedPickup = PickupCombo.SelectedItem as PicpupPoint;
                string pickupAddress = $"{selectedPickup.PickupPointPosalIndex} г. {selectedPickup.PickupPointcity} ул. {selectedPickup.PickupPointstreet} {selectedPickup.PickupPointhouse}";

                // Открываем окно талона заказа поверх окна оформления
                TicketWindow ticketWindow = new TicketWindow(currentOrder, selectedProducts, pickupAddress, TotalSumTB.Text, TotalDiscountTB.Text);
                ticketWindow.ShowDialog();

                // Очищаем корзину и закрываем окно заказа после закрытия талона
                selectedProducts.Clear();
                selectedOrderProducts.Clear();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.InnerException?.InnerException?.Message ?? ex.Message);
            }

            
        }
        
        
        private void BtnMinus_Click(object sender, RoutedEventArgs e)
        {
            var prod = (sender as Button).DataContext as Product;
            var selectedOP = selectedOrderProducts.FirstOrDefault(p => p.ProductArticleNumber == prod.ProductArticleNumber);

            if (prod.Quantity > 1)
            {
                prod.Quantity--;
                if (selectedOP != null)
                    selectedOP.QUantity--;
            }
            else
            {
                selectedProducts.Remove(prod);
                if (selectedOP != null)
                    selectedOrderProducts.Remove(selectedOP);
            }

            SetDeliveryDate();
            UpdateOrderSummary();
            ShoeListView.Items.Refresh(); // <--- БЕЗ ЭТОЙ СТРОКИ ЦИФРА НЕ ОБНОВЛЯЕТСЯ

            if (selectedProducts.Count == 0)
                this.Close();
        }

        private void BtnPlus_Click(object sender, RoutedEventArgs e)
        {
            var prod = (sender as Button).DataContext as Product;

            if (prod.Quantity >= prod.ProductQuantityInStock)
            {
                MessageBox.Show($"На складе доступно только {prod.ProductQuantityInStock} шт. этого товара.",
                                "Ограничение количества", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Прерываем выполнение, не увеличивая количество
            }

            prod.Quantity++;

            var selectedOP = selectedOrderProducts.FirstOrDefault(p => p.ProductArticleNumber == prod.ProductArticleNumber);
            if (selectedOP != null)
                selectedOP.QUantity++;

            SetDeliveryDate();
            UpdateOrderSummary();
            ShoeListView.Items.Refresh();
        }

       
    }
}

