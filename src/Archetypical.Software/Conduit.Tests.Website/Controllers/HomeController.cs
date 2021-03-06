﻿using Archetypical.Software.Conduit;
using Conduit.Tests.Website.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Conduit.Tests.Website.Controllers
{
    public class HomeController : Controller
    {
        private Archetypical.Software.Conduit.Conduit<SomeSubscriptionObject> _conduit;

        public HomeController(Archetypical.Software.Conduit.Conduit<SomeSubscriptionObject> injectedConduit)
        {
            _conduit = injectedConduit;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult TestFilter(string eventKey, string match, string message)
        {
            _conduit.SendAsync(t => !string.IsNullOrEmpty(t.Sample) && t.Sample.Equals(match), new SomePayload() { Msg = message });
            return Ok();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}