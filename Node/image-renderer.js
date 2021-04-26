"use strict";

// imports
const signalR = require("@microsoft/signalr");
require("msgpack5");
const signalRMsgPack = require("@microsoft/signalr-protocol-msgpack");
const canvas = require("canvas");

// canvas for server-side image rendering
var canv = new canvas.Canvas(800, 600);
var ctx = canv.getContext("2d");

// build (but don't start yet) the connection between the asp.net core
// web server and the node.js server-side image drawing server
var connection = new signalR
    .HubConnectionBuilder()
    .withUrl("https://localhost:5001/hubs/draw-hub")
    .withAutomaticReconnect()
    .withHubProtocol(new signalRMsgPack.MessagePackHubProtocol())
    .build();

function connect() {
    connection.invoke("NodeEstablishConnection").catch(function (err) {
        return console.error(err.toString());
    });
}

function drawLine(event) {
    ctx.beginPath();
    ctx.moveTo(event.From.X, event.From.Y);
    ctx.lineTo(event.To.X, event.To.Y);
    ctx.stroke();
}

// loads the previously stored image as the background image to draw on
function reloadImage(imageData) {
    canv = new canvas.Canvas(800, 600);
    ctx = canv.getContext("2d");
    if (imageData != "") {
        var image = new canvas.Image();
        image.src = imageData;
        ctx.drawImage(image, 0, 0);
    }
    console.log("Reloaded image.");
}

// called by the asp.net core server to request the updated image
function renderImage(room) {
    console.log("Image rendering requested.");
    connection
        .stream("RequestDrawEvents", room)
        // reloadImage() is called by the asp.net core server before
        // it writes any data into the stream for us to process
        .subscribe({
            next: (event) => {
                drawLine(event);
            },
            complete: () => {
                console.log("Finished rendering the image, sending it back to the server.");
                connection.invoke("ReceiveUpdatedImage", room, canv.toDataURL()).catch(function (err) {
                    return console.error(err.toString());
                });
            },
            error: (err) => {
                console.error(err.toString());
            }
        });
}

// start the connection
connection.start().then(async function () {
    connection.on("ReloadImage", reloadImage);
    connection.on("NodeRenderImage", renderImage);
    connection.onreconnected(connect);
    connect();
});