import {
  Coffee, Cookie, CupSoda, Drumstick, Moon, Soup, Sunrise, UtensilsCrossed, type LucideIcon,
} from 'lucide-react';

/** Picks an icon for a meal type by its name/code (Lunch, Breakfast, Dinner, Iftar, Snacks…). */
export function mealIcon(name?: string | null): LucideIcon {
  const n = (name ?? '').toLowerCase();
  if (n.includes('breakfast')) return Sunrise;
  if (n.includes('lunch')) return Soup;
  if (n.includes('dinner') || n.includes('supper')) return Drumstick;
  if (n.includes('iftar') || n.includes('sehri') || n.includes('suhoor')) return Moon;
  if (n.includes('snack')) return Cookie;
  if (n.includes('tea') || n.includes('coffee')) return Coffee;
  if (n.includes('drink') || n.includes('juice')) return CupSoda;
  return UtensilsCrossed;
}
