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

        // ����������� � ���� ������
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


        //���������� ����
        builder.Services.AddMemoryCache();
        // ���������� ���������� �������
        builder.Services.AddTransient<CeachedStopsService>();

        builder.Services.AddDistributedMemoryCache();// ��������� IDistributedMemoryCache
        builder.Services.AddSession();  // ��������� ������� ������

        var app = builder.Build();
        app.UseSession();

        // ��������� �������� � ��������
        app.MapGet("/", () =>
        {
            var content = "<h1>������ �����:</h1>" +
                "<a href=\"/info\">������� 2..1: �������� ���������� � �������</a><br/>" +
                "<a href=\"/stops\">������� 2..2: �������� ������ 20 ���������</a><br/>" +
                "<a href=\"/searchform1\">������� 2..3: �����1</a><br/>" +
                "<a href=\"/searchform2\" > ������� 2..4: �����2</a><br/>";

            return Results.Content(content, "text/html", System.Text.Encoding.UTF8);
        });
        // ������� ��� �������� �����������
        // ����� ASP.NET Core ������������� �������� ����������� ����������� � ceachedStopsService � ���������: RailwayTrafficContext � ���
        app.MapGet("/stops", (CeachedStopsService ceachedStopsService, HttpResponse response) =>
        {
            // �������� 20 ������� � ���������� ������ ���������� html ��������
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
            var clientInfo = $"<h2>���������� � �������:</h2>" +
                $"<p>IP-�����: {context.Connection.RemoteIpAddress}</p>" +
                $"<p>User-Agent: {context.Request.Headers["User-Agent"]}</p>" +
                $"<p>Host: {context.Request.Host}" +
                $"<p>Method: {context.Request.Method}";

            context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
            return context.Response.WriteAsync(clientInfo);
        });


        app.MapGet("/searchform1", (HttpContext context) =>
        {
            // ��������� �������� � ����
            context.Response.Cookies.Append("text", "text");
            context.Response.Cookies.Append("category", "category2");

            // ��������� �������� �� ����
            string text = context.Request.Cookies["text"];
            string category = context.Request.Cookies["category"];

            // �������� ����� � �������������� ���������� �� ����
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
            // ���������� ������ � ������� Session
            context.Session.SetString("searchInput", "����");
            context.Session.SetString("sortSelect", "date");
            context.Session.SetString("orderSelect", "desc");

            var searchInput = context.Session.GetString("searchInput") ?? "����� �� ���������";
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

            // ���������, ��������� �� ���� � ������ ����������� �����
            if (!allowedPaths.Contains(path))
            {
                // �������������� ������ �� '/'
                context.Response.Redirect("/");
                return;
            }

            // ���������� ��������� �������
            await next();
        });

        app.Run();



    }
}