package main

import (
	"net/http"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/chi/v5/middleware"
	"github.com/sirupsen/logrus"

	// "go.elastic.co/apm/module/apmchi/v2"
	"go.elastic.co/apm/module/apmlogrus/v2"
)

func init() {
	// apmlogrus.Hook will send "error", "panic", and "fatal" level log messages to Elastic APM.
	logrus.AddHook(&apmlogrus.Hook{})
}

func main() {
	r := chi.NewRouter()
	r.Use(middleware.Logger)
	// r.Use(apmchi.Middleware())
	r.Get("/", handleRequest)

	http.ListenAndServe(":8000", r)
}

func handleRequest(w http.ResponseWriter, req *http.Request) {
	// apmlogrus.TraceContext extracts the transaction and span (if any) from the given context,
	// and returns logrus.Fields containing the trace, transaction, and span IDs.
	traceContextFields := apmlogrus.TraceContext(req.Context())
	logrus.WithFields(traceContextFields).Debug("handling request")

	w.Write([]byte("welcome"))

	// Output:
	// {"level":"debug","msg":"handling request","time":"1970-01-01T00:00:00Z","trace.id":"67829ae467e896fb2b87ec2de50f6c0e","transaction.id":"67829ae467e896fb"}
}
