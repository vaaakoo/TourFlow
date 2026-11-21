export type Role = 'ADMINISTRATOR' | 'MANAGER' | 'OPERATOR' | 'DRIVER' | 'CUSTOMER';
export interface UserProfile {
  id: string;
  email: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  roles: Role[];
  isDisabled: boolean;
}
export interface TokenPair {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}
export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
export interface Destination {
  id: string;
  name: string;
  country: string;
  description?: string;
  imageUrl?: string;
  isActive: boolean;
}
export interface Tour {
  id: string;
  name: string;
  description: string;
  imageUrl?: string;
  durationMinutes: number;
  price: number;
  maxPassengers: number;
  destinationId: string;
  tourType: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}
export interface Customer {
  id: string;
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  createdAt: string;
}
export interface Vehicle {
  id: string;
  name: string;
  registrationNumber: string;
  vehicleType: number;
  passengerCapacity: number;
  description?: string;
  isActive: boolean;
}
export interface Driver {
  id: string;
  userId: string;
  firstName: string;
  lastName: string;
  phoneNumber: string;
  licenseNumber: string;
  isActive: boolean;
}
export interface Booking {
  id: string;
  bookingNumber: string;
  customerId: string;
  tourId?: string;
  tourName?: string;
  bookingType: number;
  bookingDate: string;
  startTime: string;
  passengerCount: number;
  pickupLocation: string;
  dropoffLocation?: string;
  specialRequests?: string;
  totalPrice: number;
  status: number;
  createdAt: string;
  updatedAt?: string;
  version: string;
}
export interface BookingHistory {
  id: string;
  previousStatus: number;
  newStatus: number;
  changedByUserId: string;
  changedAt: string;
  comment?: string;
}
export interface Assignment {
  id: string;
  bookingId: string;
  bookingNumber: string;
  driverId: string;
  driverName: string;
  vehicleId: string;
  vehicleName: string;
  assignmentDate: string;
  startTime: string;
  endTime: string;
  isActive: boolean;
  cancelledAt?: string;
}
export interface Payment {
  id: string;
  bookingId: string;
  amount: number;
  currency: string;
  paymentMethod: number;
  status: number;
  transactionReference?: string;
  paidAt?: string;
  createdAt: string;
}
export interface Dashboard {
  totalBookings: number;
  byStatus: { status: number; count: number }[];
  totalCustomers: number;
  activeTours: number;
  activeDrivers: number;
  activeVehicles: number;
  revenue: number;
  recentBookings: Booking[];
  popularTours: { tourId: string; name: string; bookings: number; revenue: number }[];
  upcomingAssignments: Assignment[];
}
export const bookingStatuses = [
  'Pending',
  'Confirmed',
  'Assigned',
  'In progress',
  'Completed',
  'Cancelled',
  'Rejected',
];
export const bookingTypes = ['Tour', 'Transfer'];
export const tourTypes = ['Sightseeing', 'Cultural', 'Adventure', 'Nature', 'Private'];
export const vehicleTypes = ['Sedan', 'SUV', 'Van', 'Minibus', 'Bus'];
export const paymentStatuses = ['Pending', 'Paid', 'Failed', 'Refunded'];
export const paymentMethods = ['Cash', 'Card', 'Bank transfer', 'Online'];
