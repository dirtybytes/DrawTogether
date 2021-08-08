"use strict";

let canvas;
let CanvasKit;
let surface;
let paint;
let snapshot;
let path;
let redrawRequested = false;

const roomID = roomModel.roomID;
var drawing = false; // whether to draw on mouse move
var lastX; //
var lastY; // helper variables for drawing lines

// initialize the connection to the server
var connection = new signalR
    .HubConnectionBuilder()
    .withUrl("/hubs/draw-hub")
    .withAutomaticReconnect()
    .build();

// drawing-related functions

function onMouseDown(event) {
    if (event.button == 0) {
        drawing = true;
        lastX = event.offsetX;
        lastY = event.offsetY;
    }
}

function onMouseUp(event) {
    if (event.button == 0) {
        drawing = false;
    }
}

function onMouseLeave(_event) {
    drawing = false;
}

function onMouseMove(event) {
    if (drawing) {
        var drawLineEvent = {
            From:
            {
                X: lastX,
                Y: lastY
            },
            To:
            {
                X: event.offsetX,
                Y: event.offsetY
            }
        };
        sendDrawLine(drawLineEvent);
        lastX = event.offsetX;
        lastY = event.offsetY;
    }
}

function sendDrawLine(ev) {
    connection.invoke("SendDrawLine", ev).catch(function (err) {
        return console.error(err.toString());
    });
}

function drawFrame(canvas) {
    if (path.countPoints() > 0 || redrawRequested) {
        redrawRequested = false;
        canvas.drawImage(snapshot, 0, 0, null);
        canvas.drawPath(path, paint);
        snapshot.delete();
        snapshot = surface.makeImageSnapshot();
        path.reset();
    }
    surface.requestAnimationFrame(drawFrame);
}

function drawLine(event) {
    path.moveTo(event.From.X, event.From.Y);
    path.lineTo(event.To.X, event.To.Y);
}

// chat-related functions

function sendChatMessage() {
    var chatText = document.getElementById("chat-text");
    connection.invoke("SendChatMessage", "Artist", chatText.value).catch(function (err) {
        return console.error(err.toString());
    });
    chatText.value = "";
}

function receiveChatMessage(user, message) {
    addChatMessage(user, message);
}

function addChatMessage(user, message) {
    var chat = document.getElementById("chat");
    var messageHtml = document.createElement("p");
    messageHtml.style = "margin-bottom:0px;";
    messageHtml.innerText = `${user}: ${message}`
    chat.appendChild(messageHtml);
}

// functions related to establishing connection/reconnecting

// called by the server as a part of establishing connection
// sets the canvas image to the most recent version cached by the server
function reloadImage(imageData) {
    snapshot.delete();
    snapshot = CanvasKit.MakeImageFromEncoded(imageData);
    redrawRequested = true;
    console.log("Succesfully resynchronized.");
}

function beginReceiveEvents() {
    connection
        .stream("ClientBeginReceiveEvents")
        .subscribe({
            next: (event) => {
                drawLine(event);
            },
            complete: () => {
                console.error("Drawing stream unexpectedly completed.");
            },
            error: (err) => {
                console.error(err.toString());
            }
        });
}

// called by the browser client
// receives a continuous stream of events to draw
async function connect() {
    await connection.invoke("Connect", roomID).catch(function (err) {
        return console.error(err.toString());
    });
    let data = [];
    connection
        .stream("SendImageToClient", roomID)
        .subscribe({
            next: (chunk) => {
                let decodedChunk = Uint8Array.from(atob(chunk), c => c.charCodeAt(0));
                data.push(decodedChunk);
            },
            complete: () => {
                var arrsSize = data.reduce((acc, el) => acc + el.length, 0);
                var newArr = new Uint8Array(arrsSize);
                data.reduce((acc, el) => { newArr.set(el, acc); return acc + el.length }, 0);
                reloadImage(newArr);
                beginReceiveEvents();
            },
            error: (err) => {
                console.error(err.toString());
            }
        });
}

connection.on("ReceiveChatMessage", receiveChatMessage);
connection.onreconnected(connect);


// start the connection
connection.start().then(async function () {
    CanvasKit = await CanvasKitInit({
        locateFile: (file) => 'https://unpkg.com/canvaskit-wasm@0.28.0/bin/' + file
    });
    surface = CanvasKit.MakeCanvasSurface("canvas");
    if (!surface) {
        console.log('Could not make surface');
        return;
    }
    paint = new CanvasKit.Paint();
    paint.setAntiAlias(true);
    paint.setColor(CanvasKit.Color(0, 0, 0, 1.0));
    paint.setStyle(CanvasKit.PaintStyle.Stroke);
    paint.setStrokeWidth(4.0);
    paint.setPathEffect(CanvasKit.PathEffect.MakeCorner(50));

    canvas = surface.getCanvas();
    snapshot = surface.makeImageSnapshot();
    path = new CanvasKit.Path();
    surface.requestAnimationFrame(drawFrame);

    await connect();

    document.getElementById("canvas").onmousedown = onMouseDown;
    document.getElementById("canvas").onmouseup = onMouseUp;
    document.getElementById("canvas").onmousemove = onMouseMove;
    document.getElementById("canvas").onmouseleave = onMouseLeave;
});