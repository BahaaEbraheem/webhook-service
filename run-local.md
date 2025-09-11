# Running the Webhook Service

## Local Development (Backend on port 8080, Frontend on port 4200)

### 1. Start Backend API
```bash
cd src/WebhookService.Api
dotnet run
# API will be available at http://localhost:8080
```

### 2. Start Frontend (in separate terminal)
```bash
cd frontend
npm install
npm run start:local
# Frontend will be available at http://localhost:4200
# API calls will be proxied to http://localhost:8080
```

## Docker Development (Backend on port 5000, Frontend on port 4200)

### 1. Start all services with Docker Compose
```bash
docker-compose up --build
# Backend API: http://localhost:5000
# Frontend: http://localhost:4200
# API calls will be proxied to http://localhost:5000
```

### 2. Test the setup
```bash
# Test backend directly
curl http://localhost:5000/health

# Test frontend (should proxy to backend)
curl http://localhost:4200/api/deliveries
```

## Troubleshooting

### Frontend can't connect to backend in Docker
- Make sure both services are in the same Docker network
- Frontend uses proxy configuration to forward API calls
- Check Docker logs: `docker-compose logs frontend`

### CORS issues
- Backend is configured to allow localhost:4200
- Check CORS configuration in Program.cs

### Port conflicts
- Local: Backend 8080, Frontend 4200
- Docker: Backend 5000 (mapped from internal 8080), Frontend 4200
