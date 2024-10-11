package main

import (
	"fmt"
	"time"

	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/push"
)

func main() {
	gatewayURL := "http://pushgateway:9091"

	completionTime := prometheus.NewGauge(prometheus.GaugeOpts{
		Name: "db_backup_last_completion_timestamp_seconds",
		Help: "The timestamp of the last successful completion of a DB backup.",
	})

	for {
		completionTime.SetToCurrentTime()
		if err := push.New(gatewayURL, "db_backup").
			Collector(completionTime).
			Grouping("db", "customers").
			Push(); err != nil {
			fmt.Println("Could not push completion time to Pushgateway:", err)
		}
		time.Sleep(7 * time.Second)
	}
}
