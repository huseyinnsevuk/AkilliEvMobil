import { PrismaClient } from '@prisma/client';
const prisma = new PrismaClient();

async function main() {
  await prisma.systemPerformanceLog.createMany({
    data: [
      { commandType: 'heater', latencyMs: 142, success: true, createdAt: new Date(Date.now() - 1000 * 60 * 60) },
      { commandType: 'fan', latencyMs: 156, success: true, createdAt: new Date(Date.now() - 1000 * 60 * 55) },
      { commandType: 'aydinlatma', latencyMs: 135, success: true, createdAt: new Date(Date.now() - 1000 * 60 * 30) },
      { commandType: 'tente', latencyMs: 180, success: true, createdAt: new Date(Date.now() - 1000 * 60 * 25) },
      { commandType: 'heater', latencyMs: 148, success: true, createdAt: new Date(Date.now() - 1000 * 60 * 10) },
      { commandType: 'fan', latencyMs: 151, success: true, createdAt: new Date(Date.now() - 1000 * 60 * 2) }
    ]
  });
  console.log('Dummy logs inserted successfully!');
}
main().catch(console.error).finally(() => prisma.$disconnect());
