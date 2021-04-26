// workaround for the self-signed certificate error
process.env["NODE_TLS_REJECT_UNAUTHORIZED"] = 0;

require("./image-renderer");