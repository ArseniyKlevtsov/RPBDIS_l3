using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RPBDIS_l3;
using RPBDIS_l3.Model;
using static System.Net.Mime.MediaTypeNames;

internal class Program
{
    private static bool cacheInitialized = false;
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Подключение к базе данных
        builder.Services.AddDbContext<RailwayTrafficContext>(options =>
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.SetBasePath(Directory.GetCurrentDirectory());
            configBuilder.AddJsonFile("appsettings.json");
            var config = configBuilder.Build();
            string? connectionString = config.GetConnectionString("RailwayTrafficConnection");
            if (connectionString == null)
            {
                throw new InvalidOperationException("Connection string is null. Please check the configuration.");
            }

            options.UseSqlServer(connectionString);
        });


        //добавление кэша
        builder.Services.AddMemoryCache();
        // Добавление созданного сервиса
        builder.Services.AddTransient<CeachedStopsService>();

        builder.Services.AddDistributedMemoryCache();// добавляем IDistributedMemoryCache
        builder.Services.AddSession();  // добавляем сервисы сессии

        var app = builder.Build();
        app.UseSession();

        // стартовая страница с ссылками
        app.MapGet("/", () =>
        {
            var content = "<h1>Список опций:</h1>" +
                "<a href=\"/info\">Задание 2..1: Получить информацию о клиенте</a><br/>" +
                "<a href=\"/stops\">Задание 2..2: Получить первые 20 остановок</a><br/>" +
                "<a href=\"/searchform1\">Задание 2..3: форма1</a><br/>" +
                "<a href=\"/searchform2\" > Задание 2..4: форма2</a><br/>";

            return Results.Content(content, "text/html", System.Text.Encoding.UTF8);
        });
        // мидлвэр для проверки кэширования
        // здесь ASP.NET Core автоматически внедряет необходимые зависимости в ceachedStopsService а конкретно: RailwayTrafficContext и кэш
        app.MapGet("/stops", (CeachedStopsService ceachedStopsService, HttpResponse response) =>
        {
            // получаем 20 записей и возвращаем строку содержащую html табличку
            var stops = ceachedStopsService.GetStops("20stops");
            var table = "<table><tr><th>Stop Id</th><th>Stop Name</th><th>Is Railway Station</th><th>Has Waiting Room</th></tr>";

            foreach (var stop in stops)
            {
                table += $"<tr><td>{stop.StopId}</td><td>{stop.StopName}</td><td>{stop.IsRailwayStation}</td><td>{stop.HasWaitingRoom}</td></tr>";
            }

            table += "</table>";

            return response.WriteAsync(table);
        });

        app.MapGet("/info", (HttpContext context) =>
        {
            var clientInfo = $"<h2>Информация о клиенте:</h2>" +
                $"<p>IP-адрес: {context.Connection.RemoteIpAddress}</p>" +
                $"<p>User-Agent: {context.Request.Headers["User-Agent"]}</p>" +
                $"<p>Host: {context.Request.Host}" +
                $"<p>Method: {context.Request.Method}";

            context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
            return context.Response.WriteAsync(clientInfo);
        });


        app.MapGet("/searchform1", (HttpContext context) =>
        {
            // Установка значений в куки
            context.Response.Cookies.Append("text", "text");
            context.Response.Cookies.Append("category", "category2");

            // Получение значений из куки
            string text = context.Request.Cookies["text"];
            string category = context.Request.Cookies["category"];

            // Создание формы с подставленными значениями из куки
            var form = $"<h2>Search Form 1</h2>" +
                $"<form action=\"/search\" method=\"GET\">" +
                $"<label for=\"searchInput\">Search Input:</label><br>" +
                $"<input type=\"text\" id=\"searchInput\" name=\"searchInput\" value=\"{text}\"><br>" +
                $"<label for=\"categorySelect\">Category:</label><br>" +
                $"<select id=\"categorySelect\" name=\"categorySelect\">" +
                $"<option value=\"category1\" {(category == "category1" ? "selected" : "")}>Category 1</option>" +
                $"<option value=\"category2\" {(category == "category2" ? "selected" : "")}>Category 2</option>" +
                $"</select><br>" +
                $"<button type=\"submit\">Search</button>" +
                $"</form>";

            context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
            return context.Response.WriteAsync(form);
        });



        app.MapGet("/searchform2", (HttpContext context) =>
        {
            // Сохранение данных в объекте Session
            context.Session.SetString("searchInput", "тект");
            context.Session.SetString("sortSelect", "date");
            context.Session.SetString("orderSelect", "desc");

            var searchInput = context.Session.GetString("searchInput") ?? "Текст по умолчанию";
            var sortSelect = context.Session.GetString("sortSelect") ?? "name";
            var orderSelect = context.Session.GetString("orderSelect") ?? "asc";

            var form = $"<h2>Search Form 2</h2>" +
                $"<form action=\"/search\" method=\"GET\">" +
                $"<label for=\"searchInput\">Search Input:</label><br>" +
                $"<input type=\"text\" id=\"searchInput\" name=\"searchInput\" value=\"{searchInput}\"><br>" +
                $"<label for=\"sortSelect\">Sort By:</label><br>" +
                $"<select id=\"sortSelect\" name=\"sortSelect\">" +
                $"<option value=\"name\" {(sortSelect == "name" ? "selected" : "")}>Name</option>" +
                $"<option value=\"date\" {(sortSelect == "date" ? "selected" : "")}>Date</option>" +
                $"</select><br>" +
                $"<label for=\"orderSelect\">Order:</label><br>" +
                $"<select id=\"orderSelect\" name=\"orderSelect\">" +
                $"<option value=\"asc\" {(orderSelect == "asc" ? "selected" : "")}>Ascending</option>" +
                $"<option value=\"desc\" {(orderSelect == "desc" ? "selected" : "")}>Descending</option>" +
                $"</select><br>" +
                $"<button type=\"submit\">Search</button>" +
                $"</form>";
            context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";


            return context.Response.WriteAsync(form);
        });
        var allowedPaths = new List<string> { "/stops", "/info", "/", "/searchform1", "/searchform2" };

        app.Use(async (context, next) =>
        {
            string path = context.Request.Path;

            // Проверяем, находится ли путь в списке разрешенных путей
            if (!allowedPaths.Contains(path))
            {
                // Перенаправляем запрос на '/'
                context.Response.Redirect("/");
                return;
            }

            // Продолжаем обработку запроса
            await next();
        });

        app.Run();



    }
}