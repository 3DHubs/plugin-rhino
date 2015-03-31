var express = require('express');
var bodyParser = require('body-parser');
var OAuthSimple = require('OAuthSimple');
var fs = require('fs');

var TEST_CLIENT_KEY = '';
var TEST_CLIENT_SECRET = '';

var app = express();
app.use(bodyParser.urlencoded({ extended: false, limit: '500mb'}));

/* Verifies the OAuth signature in a request. */
function verifySignedRequest(request) {
	var fullUrl = request.protocol + '://' + request.get('host') + request.originalUrl;

	var params = request.body;

	/* Parse the Authorization header if present. */
	var hdr = request.header('Authorization');
	if (hdr && hdr.match(/^OAuth\b/i)) {
		var matches = hdr.match(/[^=\s]+="[^"]*"(?:,\s*)?/g);
		for (var i = 0; i < matches.length; i++) {
			var match = matches[i].match(/([^=\s]+)="([^"]*)"/);
			var key = decodeURIComponent(match[1]);
			var value = decodeURIComponent(match[2]);
			params[key] = value;
		}
	}

	/* Save the request signature and exclude it from the signing input. */
	var requestSignature = params.oauth_signature;
	delete params.oauth_signature;

	/* Compute a signature for the request. */
	var ok = true, message = request.originalUrl + ": ";
	if (requestSignature) {
		oauth = new OAuthSimple(TEST_CLIENT_KEY, TEST_CLIENT_SECRET)
	    signed = oauth.sign({
	      action: request.method,
	      path: fullUrl,
	      parameters: params
	    });
	    var computedSignature = decodeURIComponent(signed.signature);

	    console.log("computed: ", computedSignature);
	    console.log("request: ", requestSignature);
	    if (requestSignature === computedSignature) {
	    	message += "OAuth signature correct.";
	    } else {
	    	message += "OAuth signature incorrect."
	    	ok = false;
	    }
	} else {
		message += "OAuth signature not present.";
	}

	console.log(message);
	return ok;
}

app.post('/api/v1/model', function (request, response) {
	verifySignedRequest(request);
	var decoded = new Buffer(request.body.file, 'base64');
	fs.writeFile("uploaded.stl", decoded, function (err) {
		if (!err)
			err = "Model file written to uploaded.stl.";
		console.log(err);
	});
	response.send({ result: "success", modelId: 12345 });
});

app.post('/api/v1/cart', function (request, response) {
	verifySignedRequest(request);
	response.send({ result: "success", url: "http://www.3dhubs.com" });
});

var server = app.listen("8080", "127.0.0.1", function () {
  var host = server.address().address;
  var port = server.address().port;
  console.log('3DHubs simulator running at http://%s:%s/api/v1.', host, port);
});

