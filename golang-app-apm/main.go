package main

import (
	"net/http"

	"github.com/sirupsen/logrus"

	"go.elastic.co/apm/module/apmlogrus/v2"
)

func init() {
	// apmlogrus.Hook will send "error", "panic", and "fatal" level log messages to Elastic APM.
	logrus.AddHook(&apmlogrus.Hook{})
}

func handleRequest(w http.ResponseWriter, req *http.Request) {
	// apmlogrus.TraceContext extracts the transaction and span (if any) from the given context,
	// and returns logrus.Fields containing the trace, transaction, and span IDs.
	traceContextFields := apmlogrus.TraceContext(req.Context())
	logrus.WithFields(traceContextFields).Debug("handling request")

	// Output:
	// {"level":"debug","msg":"handling request","time":"1970-01-01T00:00:00Z","trace.id":"67829ae467e896fb2b87ec2de50f6c0e","transaction.id":"67829ae467e896fb"}
}
