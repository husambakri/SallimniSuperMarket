using Sallimni.CustomerApp.Views;

namespace Sallimni.CustomerApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		// مسارات التصفّح والطلب.
		Routing.RegisterRoute("products", typeof(ProductsPage));
		Routing.RegisterRoute("productdetail", typeof(ProductDetailPage));
		Routing.RegisterRoute("order", typeof(OrderPage));
	}
}
