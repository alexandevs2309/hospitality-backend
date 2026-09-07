using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Hospitality.IntegrationTests;

// Una única WebApiFactory compartida: la migración y el seed corren una sola vez.
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<WebApiFactory>
{
}

[Collection("api")]
public class ApiIntegrationTests
{
    private const string AdminEmail = "admin@auronsuite.com";
    private const string AdminPassword = "Admin#2026";
    private const string UserPassword = "P@ssw0rd123";

    private readonly WebApiFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"user.{Guid.NewGuid():N}@auronsuite.com";

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await _client.SendAsync(request);
    }

    private static async Task<(JsonDocument doc, string json)> ReadBodyAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return (JsonDocument.Parse(json), json);
    }

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response)
    {
        var (doc, _) = await ReadBodyAsync(response);
        using (doc)
        {
            return doc.RootElement.GetProperty("token").GetString()!;
        }
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var (doc, _) = await ReadBodyAsync(response);
        using (doc)
        {
            return doc.RootElement.GetProperty("id").GetGuid();
        }
    }

    private async Task<(string email, string token)> RegisterAndLoginAsync()
    {
        var email = UniqueEmail();
        var register = await SendAsync(HttpMethod.Post, "/api/v1/auth/register", new
        {
            firstName = "Test",
            lastName = "User",
            email,
            phoneNumber = "+34 600 000 000",
            password = UserPassword,
            confirmPassword = UserPassword,
            department = "Front Desk",
            position = "Receptionist"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password = UserPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (email, await ReadTokenAsync(login));
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password)
        => SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password });

    private Task<HttpResponseMessage> CreateHotelAsync(string token, string name)
    {
        var hotel = new
        {
            name,
            description = $"Hotel '{name}' creado en test de integración.",
            address = "Calle Principal 1",
            phoneNumber = "+34 910 111 222",
            email = $"hotel.{Guid.NewGuid():N}@auronsuite.com",
            starRating = 3,
            totalRooms = 5,
            timeZone = "UTC",
            city = "Ciudad Test",
            country = "País Test"
        };
        return SendAsync(HttpMethod.Post, "/api/v1/hotels", hotel, token);
    }

    private async Task<Guid> CreateHotelAndGetIdAsync(string token, string name)
        => await ReadIdAsync(await CreateHotelAsync(token, name));

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Hotels_WithoutToken_ReturnsUnauthorized()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/hotels");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_WithoutToken_ReturnsUnauthorized()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/dashboard/widgets");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_Admin_ReturnsToken()
    {
        var login = await LoginAsync(AdminEmail, AdminPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadTokenAsync(login)));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var login = await LoginAsync(AdminEmail, "contraseña-incorrecta");
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidPayload_ReturnsBadRequest()
    {
        var register = await SendAsync(HttpMethod.Post, "/api/v1/auth/register", new
        {
            firstName = "X",
            lastName = "Y",
            email = "no-es-un-correo",
            password = "123",
            confirmPassword = "456"
        });
        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReadDemoDashboard()
    {
        var login = await LoginAsync(AdminEmail, AdminPassword);
        var token = await ReadTokenAsync(login);

        var hotels = await SendAsync(HttpMethod.Get, "/api/v1/hotels", token: token);
        Assert.Equal(HttpStatusCode.OK, hotels.StatusCode);

        var (hotelsBody, _) = await ReadBodyAsync(hotels);
        using (hotelsBody)
        {
            var items = hotelsBody.RootElement.GetProperty("items");
            Assert.True(items.GetArrayLength() > 0, "Debe existir el hotel demo (Hotel Aurora).");
            var hotelId = items[0].GetProperty("id").GetGuid();

            var widgets = await SendAsync(HttpMethod.Get, $"/api/v1/dashboard/widgets?hotelId={hotelId}", token: token);
            Assert.Equal(HttpStatusCode.OK, widgets.StatusCode);

            var metrics = await SendAsync(HttpMethod.Get, $"/api/v1/dashboard/metrics?hotelId={hotelId}", token: token);
            Assert.Equal(HttpStatusCode.OK, metrics.StatusCode);
        }
    }

    [Fact]
    public async Task RoomHistory_ReturnsArray_ForDemoRoom()
    {
        var login = await LoginAsync(AdminEmail, AdminPassword);
        var token = await ReadTokenAsync(login);

        var hotels = await SendAsync(HttpMethod.Get, "/api/v1/hotels", token: token);
        var (hotelsDoc, _) = await ReadBodyAsync(hotels);
        Guid firstHotelId;
        using (hotelsDoc)
        {
            var items = hotelsDoc.RootElement.GetProperty("items");
            var demo = items.EnumerateArray().FirstOrDefault(
                h => h.GetProperty("name").GetString() == "Hotel Aurora");
            firstHotelId = (demo.ValueKind == JsonValueKind.Object
                ? demo.GetProperty("id")
                : items[0].GetProperty("id")).GetGuid();
        }

        var hotelRooms = await SendAsync(HttpMethod.Get, $"/api/v1/rooms/hotel/{firstHotelId}", token: token);
        Assert.Equal(HttpStatusCode.OK, hotelRooms.StatusCode);

        var (roomsDoc, _) = await ReadBodyAsync(hotelRooms);
        Guid roomId;
        using (roomsDoc)
        {
            Assert.True(roomsDoc.RootElement.GetArrayLength() > 0, "El hotel demo debe tener habitaciones.");
            roomId = roomsDoc.RootElement[0].GetProperty("id").GetGuid();
        }

        var history = await SendAsync(HttpMethod.Get, $"/api/v1/rooms/{roomId}/history", token: token);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);

        var (historyDoc, _) = await ReadBodyAsync(history);
        using (historyDoc)
        {
            Assert.Equal(JsonValueKind.Array, historyDoc.RootElement.ValueKind);
        }

        var available = await SendAsync(HttpMethod.Get,
            $"/api/v1/rooms/available?hotelId={firstHotelId}&checkIn=2026-09-20&checkOut=2026-09-22", token: token);
        Assert.Equal(HttpStatusCode.OK, available.StatusCode);
    }

    [Fact]
    public async Task CreateHotel_WithStaleToken_DuplicateOwner_ReturnsConflict()
    {
        var (_, token) = await RegisterAndLoginAsync();
        var hotelId = await CreateHotelAndGetIdAsync(token, "Hotel Unico");
        Assert.NotEqual(Guid.Empty, hotelId);

        // Token sin claiming hotel_id (emitido antes de crear el hotel): el servicio
        // detecta el vínculo en la BD -> 409 (protección de carrera de doble creación).
        var second = await CreateHotelAsync(token, "Segundo Intento");
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task User_WithHotel_ReLogin_CannotCreateAnother()
    {
        var (email, token) = await RegisterAndLoginAsync();
        var hotelId = await CreateHotelAndGetIdAsync(token, "Hotel Unico");
        Assert.NotEqual(Guid.Empty, hotelId);

        // Re-login: el token ahora lleva el claim hotel_id -> el controlador responde 403.
        var relogin = await LoginAsync(email, UserPassword);
        var tokenWithClaim = await ReadTokenAsync(relogin);

        var second = await CreateHotelAsync(tokenWithClaim, "Otro Hotel");
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
    }

    [Fact]
    public async Task User_CannotReadAnotherHotel_Dashboard()
    {
        var (emailA, tokenA) = await RegisterAndLoginAsync();
        var hotelA = await CreateHotelAndGetIdAsync(tokenA, "Hotel A");
        var (_, tokenB) = await RegisterAndLoginAsync();
        var hotelB = await CreateHotelAndGetIdAsync(tokenB, "Hotel B");

        var reLoginA = await LoginAsync(emailA, UserPassword);
        var tokenAWithClaim = await ReadTokenAsync(reLoginA);

        var ownDashboard = await SendAsync(HttpMethod.Get, $"/api/v1/dashboard/widgets?hotelId={hotelA}", token: tokenAWithClaim);
        Assert.Equal(HttpStatusCode.OK, ownDashboard.StatusCode);

        var crossAccess = await SendAsync(HttpMethod.Get, $"/api/v1/dashboard/widgets?hotelId={hotelB}", token: tokenAWithClaim);
        Assert.Equal(HttpStatusCode.Forbidden, crossAccess.StatusCode);
    }

    [Fact]
    public async Task SoftDeletedHotel_CanBeRecreated_BySameOwner()
    {
        var (email, token) = await RegisterAndLoginAsync();
        var hotelId = await CreateHotelAndGetIdAsync(token, "Hotel Temporal");

        // Solo Admin puede borrar hoteles; el soft-delete libera el vínculo del propietario.
        var adminLogin = await LoginAsync(AdminEmail, AdminPassword);
        var adminToken = await ReadTokenAsync(adminLogin);
        var deleted = await SendAsync(HttpMethod.Delete, $"/api/v1/hotels/{hotelId}", token: adminToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // El propietario (token con claim hotel_id obsoleto) vuelve a crear otro hotel sin 409.
        var relogin = await LoginAsync(email, UserPassword);
        var tokenWithClaim = await ReadTokenAsync(relogin);

        // El soft-delete libera el vínculo del propietario: re-login sin hotel_id claim.
        var reloginAfterDelete = await LoginAsync(email, UserPassword);
        var tokenAfterDelete = await ReadTokenAsync(reloginAfterDelete);

        var recreated = await CreateHotelAsync(tokenAfterDelete, "Hotel Recreado");
        Assert.Equal(HttpStatusCode.Created, recreated.StatusCode);
        var recreatedId = await ReadIdAsync(recreated);
        Assert.NotEqual(hotelId, recreatedId);
    }
}