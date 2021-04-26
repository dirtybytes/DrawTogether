using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DrawTogetherMvc.Models;
using Microsoft.AspNetCore.SignalR;
using DrawTogetherMvc.Hubs;

namespace DrawTogetherMvc.Controllers
{
    public class DrawController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHubContext<DrawHub> _hubContext;

        public DrawController(ILogger<HomeController> logger, IHubContext<DrawHub> hubContext)
        {
            _logger = logger;
            _hubContext = hubContext;
        }
        public IActionResult Index()
        {
            return Redirect("/");
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Room(int? id)
        {
            if (id is null)
            {
                return Redirect("/");
            }
            var model = new RoomModel() { RoomID = id?.ToString() };
            return View(model);
        }
    }
}