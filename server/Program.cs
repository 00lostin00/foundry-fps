using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("FOUNDRY_LOBBY_URL") ?? "http://127.0.0.1:5080");
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions { PermitLimit=120, Window=TimeSpan.FromMinutes(1), QueueLimit=0 }));
});
var app = builder.Build();
app.UseRateLimiter();
var rooms = new ConcurrentDictionary<string, Room>();
var tickets = new ConcurrentDictionary<string, Ticket>();
var timer = new Timer(_ => {
    var now = DateTimeOffset.UtcNow;
    foreach (var (id, room) in rooms) if ((now-room.Updated).TotalSeconds>25) rooms.TryRemove(id,out var unused);
    foreach (var (id, ticket) in tickets) if (ticket.Expires<now) tickets.TryRemove(id,out var unused);
}, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
app.Lifetime.ApplicationStopped.Register(timer.Dispose);
app.MapGet("/health", () => Results.Ok(new { status="ok", version="1.0.0", rooms=rooms.Count }));
app.MapGet("/api/rooms", () => Results.Ok(new { rooms=rooms.Values.OrderBy(r=>r.Name).Select(View).ToArray() }));
app.MapPost("/api/rooms", (CreateRoom request, HttpContext context) => {
    if (rooms.Count>=128) return Results.Problem("Room limit reached",statusCode:503);
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length>40 || request.Capacity is <2 or >16 || request.Port is <1024 or >65535)
        return Results.BadRequest(new { error="Name 1–40 characters, capacity 2–16, port 1024–65535 required" });
    var address=context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
    var room = new Room { Id=Guid.NewGuid().ToString("N"), Name=request.Name.Trim(), Capacity=request.Capacity,
        Port=request.Port, Address=address, HostKey=Token(), PasswordHash=Hash(request.Password??""), Locked=!string.IsNullOrEmpty(request.Password) };
    rooms[room.Id]=room;
    return Results.Ok(new { id=room.Id, hostKey=room.HostKey, address=room.Address, port=room.Port });
});
app.MapPost("/api/rooms/{id}/heartbeat", (string id, Heartbeat request, HttpContext context) => {
    if (!rooms.TryGetValue(id,out var room)) return Results.NotFound();
    lock(room) {
        if (!Authorized(room,context)) return Results.Unauthorized();
        room.Players=Math.Clamp(request.Players,0,room.Capacity); room.Updated=DateTimeOffset.UtcNow;
    }
    return Results.Ok(new { status="ok" });
});
app.MapDelete("/api/rooms/{id}", (string id, HttpContext context) => {
    if (!rooms.TryGetValue(id,out var room)) return Results.NotFound();
    if (!Authorized(room,context)) return Results.Unauthorized();
    rooms.TryRemove(id,out var unused); return Results.NoContent();
});
app.MapPost("/api/rooms/{id}/join", (string id, JoinRoom request) => {
    if (!rooms.TryGetValue(id,out var room)) return Results.NotFound(new { error="Room expired" });
    if (string.IsNullOrWhiteSpace(request.PlayerName) || request.PlayerName.Length>24) return Results.BadRequest(new { error="Player name 1–24 characters required" });
    lock(room) {
        if (!CryptographicOperations.FixedTimeEquals(Hash(request.Password??""),room.PasswordHash)) return Results.Unauthorized();
        if (room.Players + tickets.Values.Count(t=>t.RoomId==id && t.Expires>DateTimeOffset.UtcNow)>=room.Capacity)
            return Results.Conflict(new { error="Room full" });
        var token=Token();
        tickets[token]=new Ticket(id,request.PlayerName.Trim(),DateTimeOffset.UtcNow.AddSeconds(15));
        return Results.Ok(new { address=room.Address, port=room.Port, ticket=token });
    }
});
app.MapPost("/api/rooms/{id}/admit", (string id, Admission request, HttpContext context) => {
    if (!rooms.TryGetValue(id,out var room)) return Results.NotFound();
    lock(room) {
        if (!Authorized(room,context)) return Results.Unauthorized();
        if (!tickets.TryGetValue(request.Ticket??"",out var ticket) || ticket.RoomId!=id || ticket.Expires<DateTimeOffset.UtcNow)
            return Results.Unauthorized();
        if (room.Players>=room.Capacity) return Results.Conflict();
        if (!tickets.TryRemove(request.Ticket!,out _)) return Results.Unauthorized();
        room.Players++; room.Updated=DateTimeOffset.UtcNow;
        return Results.Ok(new { playerName=ticket.PlayerName });
    }
});
app.Run();

static object View(Room r) => new { id=r.Id, name=r.Name, players=r.Players, capacity=r.Capacity, locked=r.Locked, map="FOUNDRY / 01" };
static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
static byte[] Hash(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));
static bool Authorized(Room room, HttpContext context) =>
    CryptographicOperations.FixedTimeEquals(Hash(context.Request.Headers["X-Host-Key"].ToString()),Hash(room.HostKey));
sealed class Room {
    public string Id="",Name="",HostKey="",Address="";
    public int Port,Capacity,Players=1;
    public byte[] PasswordHash=[];
    public bool Locked;
    public DateTimeOffset Updated=DateTimeOffset.UtcNow;
}
record CreateRoom(string Name,int Port,int Capacity,string? Password);
record Heartbeat(int Players);
record JoinRoom(string PlayerName,string? Password);
record Admission(string? Ticket);
record Ticket(string RoomId,string PlayerName,DateTimeOffset Expires);
