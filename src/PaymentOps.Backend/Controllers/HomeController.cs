using Microsoft.AspNetCore.Mvc;

namespace PaymentOps.Backend.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
