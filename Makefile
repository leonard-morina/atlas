# Shortcuts for working on Atlas locally (macOS/Linux). Every target is a thin wrapper around a plain command, so on
# Windows (no make by default) run the command itself; the README has them all. `make` alone lists the targets.

SERVICE ?=
NAME ?=
REPLICAS ?= 3

# Ports and passwords, the same .env Docker Compose and the AppHost read.
include .env

# onboarding | verification | accounts -> the project that holds that service's DbContext and migrations
# The service's local development database, for the migration commands that need to look at it
DATABASE = Server=localhost,$(SQL_PORT);Database=atlas_$(SERVICE);User Id=sa;Password=$(SQL_PASSWORD);TrustServerCertificate=True
INFRASTRUCTURE = src/$(shell echo $(SERVICE) | awk '{print toupper(substr($$0,1,1)) substr($$0,2)}').Infrastructure

.DEFAULT_GOAL := help
.PHONY: help up down run run-replicas stop test test-reset migration migration-remove reset logs

help: ## List the targets
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*## "} {printf "  make %-18s %s\n", $$1, $$2}'

up: ## Start the infrastructure (SQL Server, RabbitMQ, Seq, Azurite, Redis) and wait until it is healthy
	docker compose up -d --wait

down: ## Stop the infrastructure, keeping its data
	docker compose down

run: up ## Start everything: infrastructure, then all services and the Aspire dashboard
	dotnet run --project src/AppHost

run-replicas: up ## Same, with several copies of each service: make run-replicas REPLICAS=3
	dotnet run --project src/AppHost -- --Atlas:Replicas=$(REPLICAS)

stop: ## Stop a running AppHost gracefully, services included (when its terminal is gone)
	-pkill -TERM -f "artifacts/bin/AppHost/debug/AppHost"

test: up ## Run all tests, unit and integration
	dotnet test

test-reset: up ## Run all tests on test databases rebuilt from empty by the migrations
	ATLAS_TEST_RESET=true dotnet test

migration: ## Add a migration: make migration SERVICE=onboarding NAME=AddSomething
	@test -n "$(SERVICE)" -a -n "$(NAME)" || { echo "Usage: make migration SERVICE=onboarding|verification|accounts NAME=AddSomething"; exit 1; }
	dotnet tool restore
	dotnet ef migrations add $(NAME) --project $(INFRASTRUCTURE) --startup-project $(INFRASTRUCTURE) --output-dir Persistence/Migrations

migration-remove: ## Remove the last migration (not yet applied anywhere): make migration-remove SERVICE=onboarding
	@test -n "$(SERVICE)" || { echo "Usage: make migration-remove SERVICE=onboarding|verification|accounts"; exit 1; }
	dotnet tool restore
	dotnet ef migrations remove --project $(INFRASTRUCTURE) --startup-project $(INFRASTRUCTURE) -- "$(DATABASE)"

reset: ## Delete ALL local data: every database, queue, log, blob and counter (the containers' volumes)
	@read -p "This deletes all local data in SQL Server, RabbitMQ, Seq, Azurite and Redis. Continue? [y/N] " answer; [ "$$answer" = y ]
	docker compose down --volumes

logs: ## Follow the infrastructure containers' logs
	docker compose logs -f
