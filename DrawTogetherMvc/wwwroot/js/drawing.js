"use strict";

const canvas = document.getElementById('canvas');
const ctx = canvas.getContext('2d');
const roomID = roomModel.roomID;
var drawing = false; // whether to draw on mouse move
var lastX; //
var lastY; // helper variables for drawing lines

// initialize the connection to the server
var connection = new signalR
    .HubConnectionBuilder()
    .withUrl("/hubs/draw-hub")
    .withAutomaticReconnect()
    .withHubProtocol(new signalR.protocols.msgpack.MessagePackHubProtocol())
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

function drawLine(event) {
    ctx.beginPath();
    ctx.moveTo(event.From.X, event.From.Y);
    ctx.lineTo(event.To.X, event.To.Y);
    ctx.stroke();
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
    var image = document.getElementById("img-helper");
    image.src = imageData;
    document.getElementById("canvas").getContext("2d").drawImage(image, 0, 0);
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
            next: (event) => {
                data.push(event);
            },
            complete: () => {
                reloadImage(data.join(''));
                beginReceiveEvents();
            },
            error: (err) => {
                console.error(err.toString());
            }
        });
}

connection.on("ReceiveChatMessage", receiveChatMessage);
connection.on("ReloadImage", reloadImage);
connection.onreconnected(connect);

// start the connection
connection.start().then(async function () {
    await connect();
    canvas.onmousedown = onMouseDown;
    canvas.onmouseup = onMouseUp;
    canvas.onmousemove = onMouseMove;
    canvas.onmouseleave = onMouseLeave;
})
