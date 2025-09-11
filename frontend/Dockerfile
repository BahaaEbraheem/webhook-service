# Use Node.js 18 Alpine as base image
FROM node:18-alpine

# Set working directory
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies (including dev dependencies for Angular CLI)
RUN npm ci

# Copy source code
COPY . .

# Angular CLI is already in devDependencies, no need to install globally

# Expose port 4200
EXPOSE 4200

# Start the Angular development server
CMD ["npm", "start"]
