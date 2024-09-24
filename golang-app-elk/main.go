package main

import (
	"os"
	"time"

	log "github.com/sirupsen/logrus"
	"go.elastic.co/ecslogrus"
)

func main() {
	log.SetFormatter(&ecslogrus.Formatter{})
	log.SetLevel(log.TraceLevel)

	logFilePath := "./logs/out.log"
	logFile, err := os.OpenFile(logFilePath, os.O_RDWR|os.O_CREATE|os.O_APPEND, 0666)

	if err != nil {
		log.Fatal("Failed to open log file: ", err)
	}

	log.SetOutput(logFile)

	defer logFile.Close()

	log.Info("Application Started")

	for {
		log.Info("André is the best! :)")
		time.Sleep(5 * time.Second)
	}
}
