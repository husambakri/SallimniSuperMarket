using Sallimni.AdminApp.Views;

namespace Sallimni.AdminApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("productedit", typeof(ProductEditPage));
	}
}
