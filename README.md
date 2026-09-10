
# GET SOURCE FROM

# ARCHITECTURAL: 
	Clearly separated responsibilities
	Easy to maintain
	Easy to test
	Highly scalable, including in terms of functionality
	External services are isolated
	Resilience is decoupled from business logic

# OPen Windows PowerShell with admin and move to Project folder PartnerIntegrationBFF 
cd PartnerIntegrationBFF

	Start docker
	docker compose up -d

# START API SERVICES
	cd PIBFF/PIBFF.Api
	dotnet run --launch-profile http
	
# RUN WITH TEST CASE FOR REQUEST "External Service Integration"

	# Move to Project folder
	cd PartnerIntegrationBFF
	
	Unblock-File .\Test\PIBFF.Tests\TestCommand\test-partner-transaction.ps1
	
	# Test 1: Valid request
	.\Test\PIBFF.Tests\TestCommand\test-partner-transaction.ps1

	# Test transaction resulted in a negative amount. (must be return 400)
	.\Test\PIBFF.Tests\TestCommand\test-partner-transaction.ps1 -Test InvalidAmount

	# Test missing field (must be return 400)
	.\Test\PIBFF.Tests\TestCommand\test-partner-transaction.ps1 -Test MissingFields

	# Test the 30% error rate of the mock endpoint (20 calls; count 200 vs. 504 responses).
	.\test-partner-transaction.ps1 -Test MockEndpointRatio

	# Test resilience toàn pipeline (15 calls, counting 202 Accepted vs. 502 Unavailable)
	.\Test\PIBFF.Tests\TestCommand\test-partner-transaction.ps1 -Test RetryBehavior

	# Run all with test cases
	.\Test\PIBFF.Tests\TestCommand\test-partner-transaction.ps1 -Test All


# RUN WITH TEST CASE FOR REQUEST "Asynchronous Messaging"
	
	# Move to Project folder
	cd PartnerIntegrationBFF

	# Check partner with partnerId
	curl.exe -i http://localhost:5085/api/mock/partner-verification/P-1001  
	
	.\Test\PIBFF.Tests\TestCommand\test-partner-transaction.ps1 -Test All 
	
	# Test with muilti transsaction
	for ($i = 1; $i -le 20; $i++) {.\Test\PIBFF.Tests\TestCommand\test-partner-transaction.ps1 -Test All  }
	
	# Run Test case TransactionRequestValidatorTests
	dotnet test Test/PIBFF.Tests `  --filter "FullyQualifiedName~TransactionRequestValidatorTests"
	
	# Run Test case TransactionRequestValidatorTests
	dotnet test Test/PIBFF.Tests `  --filter "FullyQualifiedName~TransactionRequestValidatorTests"
	
	# Run with coverage
	dotnet test Test/PIBFF.Tests `--collect:"XPlat Code Coverage"
	dotnet tool install -g dotnet-reportgenerator-globaltool
	
	reportgenerator `
	  "-reports:Test/PIBFF.Tests/TestResults/**/coverage.cobertura.xml" `
	  "-targetdir:coverage-report" `
	  "-reporttypes:Html"
  
	start .\coverage-report\index.html
	
	# Check RabbitMQ
	 docker exec partner-integration-rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged consumers

	# Can you see detail in  with RabbitMQ
	
	
		
	login:
		username:guest
		password:guest
		
	view
	http://localhost:15672/#/queues
	 
	