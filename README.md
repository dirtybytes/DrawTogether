# DrawTogether
A very simple website project using signalR and ASP.NET for drawing online with multiple users on the same canvas.
This project consists of two parts: the ASP.NET web server, and a Node.js local client used to render images server-side.
## Using the website
* Running the ASP.NET server
```
cd DrawTogetherMvc
dotnet run
```
* Running the Node.js worker
```
cd Node
npm install
npm start
```
Optionally, if you haven't done this already, run the following command to setup the self-signed security certificate:
```
dotnet dev-certs https --trust
```
Then navigate to [https://localhost:5001/](https://localhost:5001/) in your browser, create a drawing room, open a second tab in the browser with the same URL and notice how the image is modified in the same way in both tabs. Try to reload the browser tab and notice that the image is persistent between reloads (sometimes it requires you to reload the page twice to see the changes).
