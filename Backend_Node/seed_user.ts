import { PrismaClient } from '@prisma/client';
import { Pool } from 'pg';
import { PrismaPg } from '@prisma/adapter-pg';
import dotenv from 'dotenv';

dotenv.config();

const pool = new Pool({ connectionString: process.env.DATABASE_URL });
const adapter = new PrismaPg(pool as any);
const prisma = new PrismaClient({ adapter });

async function main() {
  const user = await prisma.user.create({
    data: {
      fullName: 'Hüseyin Sevük',
      email: 'huseyin@example.com',
      phoneNumber: '5551234567',
      passwordHash: 'dummy_hash_for_now',
      isEmailVerified: true,
      isPhoneVerified: true,
      isActive: true,
      subscriptionType: 'Premium'
    }
  });
  console.log('User created:', user);
}

main()
  .catch(e => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
